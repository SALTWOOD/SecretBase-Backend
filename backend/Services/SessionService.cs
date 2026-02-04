using backend.Tables;
using SqlSugar;
using StackExchange.Redis;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace backend.Services;

public class SessionService
{
    private readonly IDatabase _redis;
    private readonly SettingService _setting;
    private const string SessionPrefix = "user_session:";

    public SessionService(IConnectionMultiplexer redis, SettingService setting)
    {
        _redis = redis.GetDatabase();
        _setting = setting;
    }

    public async Task<string> CreateSessionAsync(UserTable user)
    {
        var hours = await _setting.Get<int>(SettingKeys.Site.Security.Cookie.ExpireHours);
        var token = Utils.GenerateRandomSecret(64);
        var key = $"{SessionPrefix}{token}";

        var sessionData = new
        {
            user.Id,
            user.Role,
            CreatedAt = DateTime.UtcNow
        };

        await _redis.StringSetAsync(key, JsonSerializer.Serialize(sessionData), TimeSpan.FromHours(hours));

        return token;
    }

    public async Task<ClaimsPrincipal?> ValidateSessionAsync(string token)
    {
        var data = await _redis.StringGetAsync($"{SessionPrefix}{token}");
        if (data.IsNullOrEmpty) return null;

        var session = JsonSerializer.Deserialize<JsonElement>(data.ToString());

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, session.GetProperty("Id").GetInt32().ToString()),
            new Claim(ClaimTypes.Role, session.GetProperty("Role").GetInt32().ToString())
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "SimpleSession"));
    }

    public async Task RemoveSessionAsync(string token)
    {
        await _redis.KeyDeleteAsync($"{SessionPrefix}{token}");
    }
}