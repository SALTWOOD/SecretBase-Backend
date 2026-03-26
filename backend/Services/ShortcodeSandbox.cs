using backend.Database;
using backend.Database.Entities;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace backend.Services;

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

/// <summary>
/// 安全的数据库访问
/// </summary>
public class SafeDbAccess
{
    private readonly AppDbContext _db;
    private readonly string _shortcodeName;
    private readonly ILogger _logger;

    public SafeDbAccess(AppDbContext db, string shortcodeName, ILogger logger)
    {
        _db = db;
        _shortcodeName = shortcodeName;
        _logger = logger;
    }

    // 提供只读查询能力
    // 注意：这里简化实现，实际应该限制可访问的表和操作
    public async Task<object?> FindByIdAsync(string entityType, int id)
    {
        _logger.LogDebug("[{Shortcode}] FindByIdAsync: {EntityType} {Id}", _shortcodeName, entityType, id);
        return entityType.ToLower() switch
        {
            "user" => await _db.Users.FindAsync(id),
            "article" => await _db.Articles.FindAsync(id),
            "comment" => await _db.Comments.FindAsync(id),
            _ => null
        };
    }
}

/// <summary>
/// 安全的 Redis 访问
/// </summary>
public class SafeRedisAccess
{
    private readonly IDatabase _redis;
    private readonly string _keyPrefix;

    public SafeRedisAccess(IDatabase redis, string shortcodeName)
    {
        _redis = redis;
        _keyPrefix = $"shortcode:{shortcodeName}:";
    }

    public async Task<string?> GetAsync(string key)
    {
        var value = await _redis.StringGetAsync(_keyPrefix + key);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task SetAsync(string key, string value, int? expirySeconds = null)
    {
        if (expirySeconds.HasValue)
        {
            await _redis.StringSetAsync(_keyPrefix + key, value, TimeSpan.FromSeconds(expirySeconds.Value));
        }
        else
        {
            await _redis.StringSetAsync(_keyPrefix + key, value);
        }
    }

    public async Task DeleteAsync(string key)
    {
        await _redis.KeyDeleteAsync(_keyPrefix + key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await _redis.KeyExistsAsync(_keyPrefix + key);
    }
}

/// <summary>
/// 安全的日志记录器
/// </summary>
public class SafeLogger
{
    private readonly ILogger _logger;
    private readonly string _shortcodeName;

    public SafeLogger(ILogger logger, string shortcodeName)
    {
        _logger = logger;
        _shortcodeName = shortcodeName;
    }

    public void Info(string message, object? data = null)
    {
        _logger.LogInformation("[{Shortcode}] {Message} {@Data}", _shortcodeName, message, data);
    }

    public void Warn(string message, object? data = null)
    {
        _logger.LogWarning("[{Shortcode}] {Message} {@Data}", _shortcodeName, message, data);
    }

    public void Error(string message, object? data = null)
    {
        _logger.LogError("[{Shortcode}] {Message} {@Data}", _shortcodeName, message, data);
    }
}