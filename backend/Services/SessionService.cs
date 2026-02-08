using backend.Tables;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace backend.Services;

public static class Permissions
{
    public static string All = "*";
    public static string User = "user:*";
    public static string UserRead = "user:read";
    public static string UserWrite = "user:write";
    public static string Admin = "admin:*";
    public static string AdminRead = "admin:read";
    public static string AdminWrite = "admin:write";
}

public readonly record struct SessionData(
    int Id,
    UserRole Role,
    HashSet<string> Access,
    DateTime CreatedAt
);

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

    public async Task<(string, int)> CreateSessionAsync(UserTable user, HashSet<string>? access = null, int? expireHours = null)
    {
        if (access == null) access = [Permissions.All];
        var hours = expireHours.HasValue ? expireHours.Value : await _setting.Get<int>(SettingKeys.Site.Security.Cookie.ExpireHours);
        var token = Utils.GenerateRandomSecret(64);
        var key = $"{SessionPrefix}{token}";

        SessionData sessionData = new SessionData
        {
            Id = user.Id,
            Role = user.Role,
            Access = access,
            CreatedAt = DateTime.UtcNow
        };

        await _redis.StringSetAsync(key, JsonSerializer.Serialize(sessionData), TimeSpan.FromHours(hours));

        return (token, hours);
    }

    public async Task<ClaimsPrincipal?> ValidateSessionAsync(string token)
    {
        var data = await _redis.StringGetAsync($"{SessionPrefix}{token}");
        if (data.IsNullOrEmpty) return null;

        var session = JsonSerializer.Deserialize<SessionData>(data.ToString());

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, session.Id.ToString()),
            new Claim(ClaimTypes.Role, session.Role.ToString())
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "SimpleSession"));
    }

    public async Task RemoveSessionAsync(string token)
    {
        await _redis.KeyDeleteAsync($"{SessionPrefix}{token}");
    }
}