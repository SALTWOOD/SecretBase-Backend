using backend.Tables;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace backend.Services;

public class SessionService
{
    private readonly IDatabase _redis;
    private const string SessionPrefix = "user_session:";

    public SessionService(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    public async Task<string> CreateSessionAsync(UserTable user)
    {
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var key = $"{SessionPrefix}{token}";

        var sessionData = new
        {
            user.Id,
            user.Role,
            CreatedAt = DateTime.UtcNow
        };

        await _redis.StringSetAsync(key, JsonSerializer.Serialize(sessionData), TimeSpan.FromHours(24));

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
            new Claim(ClaimTypes.Role, session.GetProperty("Role").GetString() ?? "User")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "SimpleCookie"));
    }

    public async Task RemoveSessionAsync(string token)
    {
        await _redis.KeyDeleteAsync($"{SessionPrefix}{token}");
    }
}