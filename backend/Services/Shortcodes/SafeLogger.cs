namespace backend.Services.Shortcodes;

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