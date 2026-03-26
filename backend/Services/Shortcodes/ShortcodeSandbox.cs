using backend.Database;
using backend.Database.Entities;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace backend.Services.Shortcodes;

public class ShortcodeSandbox
{
    private readonly AppDbContext _db;
    private readonly IDatabase _redis;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ShortcodeSandbox> _logger;
    private readonly TimeSpan _executionTimeout = TimeSpan.FromSeconds(30);

    public ShortcodeSandbox(
        AppDbContext db,
        IConnectionMultiplexer redis,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment env,
        ILogger<ShortcodeSandbox> logger)
    {
        _db = db;
        _redis = redis.GetDatabase();
        _httpClientFactory = httpClientFactory;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// 执行指定的 handler 函数
    /// </summary>
    public async Task<object?> ExecuteHandlerAsync(
        string backendCode,
        string handlerName,
        JsonElement requestBody,
        Dictionary<string, string> headers,
        Dictionary<string, string> query,
        User? currentUser)
    {
        try
        {
            var engine = CreateEngine(handlerName, currentUser);
            await engine.ExecuteAsync(backendCode);

            var request = CreateRequestObject(engine, requestBody, headers, query, currentUser);
            var jsValue = await engine.InvokeAsync(handlerName, request);
            
            if (jsValue.IsPromise())
                jsValue = jsValue.UnwrapIfPromise();

            return jsValue.ToObject();
        }
        catch (JavaScriptException ex)
        {
            _logger.LogError(ex, "JavaScript error in shortcode handler: {HandlerName}", handlerName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing shortcode handler: {HandlerName}", handlerName);
            throw;
        }
    }

    /// <summary>
    /// 验证 handler 是否存在
    /// </summary>
    public bool HandlerExists(string backendCode, string handlerName)
    {
        try
        {
            var engine = new Engine(options => { options.Strict(); });

            engine.Execute(backendCode);
            var handlerValue = engine.GetValue(handlerName);

            if (handlerValue == null || handlerValue == JsValue.Undefined)
            {
                return false;
            }

            return handlerValue.IsObject();
        }
        catch
        {
            return false;
        }
    }

    private Engine CreateEngine(string shortcodeName, User? currentUser)
    {
        var engine = new Engine(options =>
        {
            options.TimeoutInterval(_executionTimeout);
            options.Strict();
            options.AllowClr();
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
        });

        engine.SetValue("context", CreateSafeContext(shortcodeName, currentUser));
        engine.SetValue("console", CreateSafeConsole());

        return engine;
    }

    private object CreateSafeContext(string shortcodeName, User? currentUser)
    {
        return new
        {
            db = new SafeDbAccess(_db, shortcodeName, _logger),
            redis = new SafeRedisAccess(_redis, shortcodeName),
            http = new SafeHttpAccess(_httpClientFactory, shortcodeName, _logger),
            currentUser = currentUser != null
                ? new
                {
                    id = currentUser.Id,
                    username = currentUser.Username,
                    role = currentUser.Role.ToString()
                }
                : null,
            logger = new SafeLogger(_logger, shortcodeName)
        };
    }

    private class JintConsole
    {
        private readonly ILogger _logger;
        public JintConsole(ILogger logger) => _logger = logger;

        public void log(params object[] args) =>
            _logger.LogInformation("[Shortcode] {Args}", string.Join(", ", args));

        public void info(params object[] args) => log(args);

        public void warn(params object[] args) =>
            _logger.LogWarning("[Shortcode] {Args}", string.Join(", ", args));

        public void error(params object[] args) =>
            _logger.LogError("[Shortcode] {Args}", string.Join(", ", args));
    }

    private object CreateSafeConsole() => new JintConsole(_logger);

    private JsValue CreateRequestObject(
        Engine engine,
        JsonElement requestBody,
        Dictionary<string, string> headers,
        Dictionary<string, string> query,
        User? currentUser)
    {
        var requestObj = new Dictionary<string, object?>
        {
            ["body"] = ConvertJsonElementToObject(requestBody),
            ["headers"] = headers,
            ["query"] = query,
            ["currentUser"] = currentUser != null
                ? new
                {
                    id = currentUser.Id,
                    username = currentUser.Username,
                    role = currentUser.Role.ToString()
                }
                : null
        };

        return JsValue.FromObject(engine, requestObj);
    }

    private static object? ConvertJsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElementToObject(p.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertJsonElementToObject)
                .ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => null
        };
    }
}