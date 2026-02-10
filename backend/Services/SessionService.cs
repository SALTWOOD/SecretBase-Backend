using backend.Controllers;
using backend.Database.Entities;
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
    public static HashSet<string> None => new HashSet<string>();

    /// <summary>
    /// Permission set for full permission token
    /// </summary>
    public static HashSet<string> Full => new HashSet<string> { Permissions.All };
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
    private readonly SettingService _setting;
    private const string SessionPrefix = "user_session:";

    public SessionService(IConnectionMultiplexer redis, SettingService setting)
    {
        _redis = redis.GetDatabase();
        _setting = setting;
    }

    public async Task<(string, int)> CreateSessionAsync(User user, HashSet<string>? access = null, int? expireHours = null, TokenPermissionLevel permissionLevel = TokenPermissionLevel.Full)
    {
        if (access == null) access = [Permissions.All];
        var hours = expireHours.HasValue ? expireHours.Value : await _setting.Get<int>(SettingKeys.Site.Security.Cookie.ExpireHours);
        var token = Utils.GenerateRandomSecret(64);
        var key = $"{SessionPrefix}{token}";

        SessionData sessionData = new SessionData
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

        // Get current remaining time to live
        var ttl = await _redis.KeyTimeToLiveAsync(key);
        if (ttl.HasValue)
        {
            await _redis.StringSetAsync(key, JsonSerializer.Serialize(upgradedSession), ttl.Value);
        }

        return true;
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
            new Claim(ClaimTypes.NameIdentifier, session.Id.ToString()),
            new Claim(ClaimTypes.Name, session.Username),
            new Claim(ClaimTypes.Role, session.Role.ToString())
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "SimpleSession"));
    }

    public async Task RemoveSessionAsync(string token)
    {
        await _redis.KeyDeleteAsync($"{SessionPrefix}{token}");
    }
}