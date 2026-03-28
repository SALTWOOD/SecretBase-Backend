namespace backend.Types;

/// <summary>
/// Rate limiter configuration options
/// </summary>
public class RateLimiterOptions
{
    /// <summary>
    /// Whether to enable rate limiting. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Time window for rate limiting. Default: 1 minute.
    /// </summary>
    public int WindowSeconds { get; set; } = 60;
    
    /// <summary>
    /// Maximum number of requests per window. Default: 60.
    /// </summary>
    public int PermitLimit { get; set; } = 60;
    
    /// <summary>
    /// Maximum number of queued requests. Default: 5.
    /// </summary>
    public int QueueLimit { get; set; } = 5;
}