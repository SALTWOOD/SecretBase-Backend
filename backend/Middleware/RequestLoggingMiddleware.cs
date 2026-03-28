using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace backend.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // 记录请求开始时间
        var stopwatch = Stopwatch.StartNew();

        // 读取请求体（需要重置流以便后续处理）
        var requestBody = await ReadRequestBodyAsync(context);

        // 记录请求信息
        var requestInfo = new
        {
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Method = context.Request.Method,
            Path = context.Request.Path,
            QueryString = context.Request.QueryString.ToString(),
            Scheme = context.Request.Scheme,
            Host = context.Request.Host.ToString(),
            Protocol = context.Request.Protocol,
            RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString(),
            RemotePort = context.Connection.RemotePort,
            LocalIpAddress = context.Connection.LocalIpAddress?.ToString(),
            LocalPort = context.Connection.LocalPort,
            Headers = RedactSensitiveHeaders(context.Request.Headers),
            ContentType = context.Request.ContentType,
            ContentLength = context.Request.ContentLength,
            Cookies = RedactSensitiveCookies(context.Request.Cookies),
            Body = RedactSensitiveBody(requestBody),
            User = context.User?.Identity?.Name ?? "Anonymous",
            IsAuthenticated = context.User?.Identity?.IsAuthenticated ?? false
        };

        logger.LogInformation("=== HTTP REQUEST ===\n{RequestInfo}",
            JsonSerializer.Serialize(requestInfo, new JsonSerializerOptions { WriteIndented = true }));

        // 捕获响应
        var originalBodyStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while processing the request");
            throw;
        }

        stopwatch.Stop();

        // 读取响应体
        memoryStream.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

        // 重置流并复制响应
        memoryStream.Seek(0, SeekOrigin.Begin);
        await memoryStream.CopyToAsync(originalBodyStream);
        context.Response.Body = originalBodyStream;

        // 记录响应信息
        var responseInfo = new
        {
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            StatusCode = context.Response.StatusCode,
            ContentType = context.Response.ContentType,
            ContentLength = context.Response.ContentLength,
            Headers = RedactSensitiveHeaders(context.Response.Headers),
            Body = RedactSensitiveBody(responseBody),
            Duration = stopwatch.ElapsedMilliseconds,
            DurationFormatted = stopwatch.Elapsed.ToString(@"hh\:mm\:ss\.fff")
        };

        logger.LogInformation("=== HTTP RESPONSE ===\n{ResponseInfo}",
            JsonSerializer.Serialize(responseInfo, new JsonSerializerOptions { WriteIndented = true }));

        logger.LogInformation("=== REQUEST COMPLETED IN {Duration}ms ===", stopwatch.ElapsedMilliseconds);
    }

    private async Task<string> ReadRequestBodyAsync(HttpContext context)
    {
        // 如果请求体为空或已经被读取过，返回空字符串
        if (context.Request.ContentLength == 0 || context.Request.ContentLength == null) return string.Empty;

        // 对于某些内容类型（如 multipart/form-data），不尝试读取请求体
        if (context.Request.ContentType != null &&
            (context.Request.ContentType.Contains("multipart/form-data") ||
             context.Request.ContentType.Contains("image/") ||
             context.Request.ContentType.Contains("video/") ||
             context.Request.ContentType.Contains("audio/")))
            return $"[Body content not logged for Content-Type: {context.Request.ContentType}]";

        try
        {
            context.Request.EnableBuffering();

            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                false,
                1024,
                true);

            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            // 如果请求体太大，截断显示
            const int maxBodyLength = 10000;
            if (body.Length > maxBodyLength)
                return body.Substring(0, maxBodyLength) + $"... [truncated, total length: {body.Length}]";

            return body;
        }
        catch (Exception ex)
        {
            return $"[Error reading request body: {ex.Message}]";
        }
    }

    /// <summary>
    /// Redact sensitive headers (authorization, cookie, token, etc.)
    /// </summary>
    private static Dictionary<string, string> RedactSensitiveHeaders(IHeaderDictionary headers)
    {
        var result = new Dictionary<string, string>();
        var sensitiveHeaders = new[] { "authorization", "cookie", "token", "api-key", "apikey", "secret" };

        foreach (var header in headers)
        {
            var headerKey = header.Key.ToLowerInvariant();
            if (sensitiveHeaders.Any(sh => headerKey.Contains(sh)))
                result[header.Key] = "[REDACTED]";
            else
                result[header.Key] = header.Value.ToString();
        }

        return result;
    }

    /// <summary>
    /// Redact sensitive cookies (auth, token, session, etc.)
    /// </summary>
    private static Dictionary<string, string> RedactSensitiveCookies(IRequestCookieCollection cookies)
    {
        var result = new Dictionary<string, string>();
        var sensitiveCookies = new[] { "auth", "token", "session", "jwt", "sid", "csrf", "xsrf" };

        foreach (var cookie in cookies)
        {
            var cookieKey = cookie.Key.ToLowerInvariant();
            if (sensitiveCookies.Any(sc => cookieKey.Contains(sc)))
                result[cookie.Key] = "[REDACTED]";
            else
                result[cookie.Key] = cookie.Value;
        }

        return result;
    }

    /// <summary>
    /// Redact sensitive content from request body (passwords, secrets, etc.)
    /// </summary>
    private static string RedactSensitiveBody(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;

        var lowerBody = body.ToLowerInvariant();
        var sensitiveKeywords = new[]
            { "password", "secret", "token", "api_key", "apikey", "authorization", "credential" };

        if (sensitiveKeywords.Any(kw => lowerBody.Contains(kw))) return "[SENSITIVE CONTENT REDACTED]";

        return body;
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}