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
/// Token 权限级别
/// </summary>
public enum TokenPermissionLevel
{
    /// <summary>
    /// 无权限 token，只能在登录时使用，需要通过 2FA 验证升级
    /// </summary>
    None = 0,

    /// <summary>
    /// 完全权限 token，可以读写数据
    /// </summary>
    Full = 1
}

public static class TokenPermissions
{
    /// <summary>
    /// 无权限 token 的权限集合
    /// </summary>
    public static HashSet<string> None => new HashSet<string>();

    /// <summary>
    /// 完全权限 token 的权限集合
    /// </summary>
    public static HashSet<string> Full => new HashSet<string> { Permissions.All };
}

public readonly record struct SessionData(
    int Id,
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
            Role = user.Role,
            Access = access,
            CreatedAt = DateTime.UtcNow,
            PermissionLevel = permissionLevel
        };

        await _redis.StringSetAsync(key, JsonSerializer.Serialize(sessionData), TimeSpan.FromHours(hours));

        return (token, hours);
    }

    /// <summary>
    /// 升级 token 权限级别
    /// </summary>
    /// <param name="token">要升级的 token</param>
    /// <returns>是否升级成功</returns>
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
            Role = session.Role,
            Access = TokenPermissions.Full,
            CreatedAt = session.CreatedAt,
            PermissionLevel = TokenPermissionLevel.Full
        };

        // 获取当前剩余过期时间
        var ttl = await _redis.KeyTimeToLiveAsync(key);
        if (ttl.HasValue)
        {
            await _redis.StringSetAsync(key, JsonSerializer.Serialize(upgradedSession), ttl.Value);
        }

        return true;
    }

    /// <summary>
    /// 获取 token 的权限级别
    /// </summary>
    /// <param name="token">token</param>
    /// <returns>权限级别，如果 token 不存在返回 null</returns>
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
            new Claim(ClaimTypes.Role, session.Role.ToString())
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "SimpleSession"));
    }

    public async Task RemoveSessionAsync(string token)
    {
        await _redis.KeyDeleteAsync($"{SessionPrefix}{token}");
    }
}