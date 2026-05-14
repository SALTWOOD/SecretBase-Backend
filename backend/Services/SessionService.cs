using backend.Controllers;
using backend.Database.Entities;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace backend.Services;

public static class Permissions
{
    public const string All = "*";
    public const string User = "user:*";
    public const string UserRead = "user:read";
    public const string UserWrite = "user:write";
    public const string Admin = "admin:*";
    public const string AdminRead = "admin:read";
    public const string AdminWrite = "admin:write";
}

/// <summary>
/// Token permission level
/// </summary>
public enum TokenPermissionLevel
{
    /// <summary>
    /// No permission token, can only be used during login, needs to be upgraded through 2FA verification
    /// </summary>
    None = 0,

    /// <summary>
    /// Full permission token, can read and write data
    /// </summary>
    Full = 1
}

public static class TokenPermissions
{
    /// <summary>
    /// Permission set for no permission token
    /// </summary>
    public static HashSet<string> None => new();

    /// <summary>
    /// Permission set for full permission token
    /// </summary>
    public static HashSet<string> Full => new() { Permissions.All };
}

public readonly record struct SessionData(
    int Id,
    string Username,
    UserRole Role,
    HashSet<string> Access,
    DateTime CreatedAt,
    TokenPermissionLevel PermissionLevel
);

public class SessionService
{
    private readonly IDatabase _redis;
    private const string SessionPrefix = "user_session:";

    public SessionService(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    public async Task<(string, int)> CreateSessionAsync(User user, HashSet<string>? access = null,
        int? expireHours = null, TokenPermissionLevel permissionLevel = TokenPermissionLevel.Full)
    {
        if (access == null) access = [Permissions.All];
        var hours = expireHours.HasValue ? expireHours.Value : await SettingRegistry.Site.Security.Cookie.ExpireHours;
        var token = Utils.GenerateRandomSecret(64);
        var key = $"{SessionPrefix}{token}";

        var sessionData = new SessionData
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role,
            Access = access,
            CreatedAt = DateTime.UtcNow,
            PermissionLevel = permissionLevel
        };

        await _redis.StringSetAsync(key, JsonSerializer.Serialize(sessionData), TimeSpan.FromHours(hours));

        return (token, hours);
    }

    /// <summary>
    /// Upgrade token permission level
    /// </summary>
    /// <param name="token">The token to upgrade</param>
    /// <returns>Whether the upgrade was successful</returns>
    public async Task<bool> UpgradeTokenAsync(string token)
    {
        var key = $"{SessionPrefix}{token}";
        var data = await _redis.StringGetAsync(key);
        if (data.IsNullOrEmpty) return false;

        var session = JsonSerializer.Deserialize<SessionData>(data.ToString());
        if (session.PermissionLevel == TokenPermissionLevel.Full) return false;

        var upgradedSession = new SessionData
        {
            Id = session.Id,
            Username = session.Username,
            Role = session.Role,
            Access = TokenPermissions.Full,
            CreatedAt = session.CreatedAt,
            PermissionLevel = TokenPermissionLevel.Full
        };

        // Atomic compare-and-swap: only upgrade if the data hasn't changed
        var originalJson = data.ToString();
        var upgradedJson = JsonSerializer.Serialize(upgradedSession);
        var script = @"
local current = redis.call('GET', KEYS[1])
if current == ARGV[1] then
    local ttl = redis.call('TTL', KEYS[1])
    if ttl > 0 then
        redis.call('SET', KEYS[1], ARGV[2], 'EX', ttl)
        return 1
    end
end
return 0";
        var result = (long)await _redis.ScriptEvaluateAsync(script,
            [key],
            [originalJson, upgradedJson]);
        return result == 1;
    }

    /// <summary>
    /// Get token permission level
    /// </summary>
    /// <param name="token">token</param>
    /// <returns>Permission level, returns null if token doesn't exist</returns>
    public async Task<TokenPermissionLevel?> GetTokenPermissionLevelAsync(string token)
    {
        var data = await _redis.StringGetAsync($"{SessionPrefix}{token}");
        if (data.IsNullOrEmpty) return null;

        var session = JsonSerializer.Deserialize<SessionData>(data.ToString());
        return session.PermissionLevel;
    }

    public async Task<ClaimsPrincipal?> ValidateSessionAsync(string token)
    {
        var data = await _redis.StringGetAsync($"{SessionPrefix}{token}");
        if (data.IsNullOrEmpty) return null;

        var session = JsonSerializer.Deserialize<SessionData>(data.ToString());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.Id.ToString()),
            new(ClaimTypes.Name, session.Username),
            new(ClaimTypes.Role, session.Role.ToString())
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "SimpleSession"));
    }

    public async Task RemoveSessionAsync(string token)
    {
        await _redis.KeyDeleteAsync($"{SessionPrefix}{token}");
    }
}