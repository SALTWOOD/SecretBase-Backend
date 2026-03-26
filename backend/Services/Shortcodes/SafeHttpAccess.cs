using System.Text.Json;

namespace backend.Services.Shortcodes;

public class SafeHttpAccess
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _shortcodeName;
    private readonly ILogger _logger;

    public SafeHttpAccess(IHttpClientFactory httpClientFactory, string shortcodeName, ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _shortcodeName = shortcodeName;
        _logger = logger;
    }

    public async Task<object?> GetAsync(string url, Dictionary<string, string>? headers = null)
    {
        return await SendRequestAsync(HttpMethod.Get, url, null, headers);
    }

    public async Task<object?> PostAsync(string url, object body, Dictionary<string, string>? headers = null)
    {
        return await SendRequestAsync(HttpMethod.Post, url, body, headers);
    }

    private async Task<object?> SendRequestAsync(HttpMethod method, string url, object? body = null, Dictionary<string, string>? headers = null)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("ShortcodeExecution");
            // 设置默认超时，防止 JS 任务挂起整个线程池
            client.Timeout = TimeSpan.FromSeconds(10);

            var request = new HttpRequestMessage(method, url);

            if (headers != null)
            {
                foreach (var header in headers)
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (body != null)
            {
                var json = JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            }

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[{Shortcode}] Http {Method} failed: {Url} - {StatusCode}", 
                    _shortcodeName, method, url, response.StatusCode);
            }

            try 
            {
                using var doc = JsonDocument.Parse(content);
                return content;
            }
            catch { return content; }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Shortcode}] Http request error: {Url}", _shortcodeName, url);
            return null;
        }
    }
}