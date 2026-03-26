using StackExchange.Redis;

namespace backend.Services.Shortcodes;

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