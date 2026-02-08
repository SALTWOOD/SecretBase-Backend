using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace backend.Services;

public class TwoFactorManager
{
    private readonly IDatabase _redis;
    private const string PREFIX = "auth:2fa_approved:";

    public TwoFactorManager(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    private string GetSessionSuffix(string authToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(authToken));
        return Convert.ToHexString(hash);
    }

    public async Task GrantGracePeriodAsync(int userId, string authToken, int minutes = 10)
    {
        var key = $"{PREFIX}{userId}:{GetSessionSuffix(authToken)}";
        await _redis.StringSetAsync(key, "true", TimeSpan.FromMinutes(minutes));
    }

    public async Task<bool> IsApprovedAsync(int userId, string authToken)
    {
        var key = $"{PREFIX}{userId}:{GetSessionSuffix(authToken)}";
        return await _redis.KeyExistsAsync(key);
    }
}