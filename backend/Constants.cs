namespace backend;

public static class Constants
{
    public const string AUTH_TOKEN_COOKIE_NAME = "auth_token";
    public const string DEFAULT_AVATAR_URL = "/default_avatar.webp";
}

/// <summary>
/// OAuth 2.0 / OpenID Connect Scopes
/// 定义项目支持的所有 OAuth scope
/// </summary>
public static class OAuthScopes
{
    // OpenID Connect 标准 scopes
    /// <summary>
    /// OpenID Connect 标识符，必须包含以启用 OpenID Connect
    /// </summary>
    public const string OpenId = "openid";

    /// <summary>
    /// 访问用户邮箱地址
    /// </summary>
    public const string Email = "email";

    /// <summary>
    /// 访问用户基本信息（用户名、头像等）
    /// </summary>
    public const string Profile = "profile";

    /// <summary>
    /// 获取刷新令牌以延长访问权限
    /// </summary>
    public const string OfflineAccess = "offline_access";

    // 项目自定义 scopes
    /// <summary>
    /// 访问用户角色信息
    /// </summary>
    public const string Roles = "roles";

    /// <summary>
    /// 访问用户创建的 OAuth 应用列表
    /// </summary>
    public const string Apps = "apps";

    /// <summary>
    /// 访问用户的 OAuth 授权历史
    /// </summary>
    public const string Consents = "consents";

    /// <summary>
    /// 访问用户的活跃令牌列表
    /// </summary>
    public const string Tokens = "tokens";

    /// <summary>
    /// 读取用户设置信息
    /// </summary>
    public const string SettingsRead = "settings:read";

    /// <summary>
    /// 修改用户设置信息
    /// </summary>
    public const string SettingsWrite = "settings:write";

    /// <summary>
    /// 读取用户邀请码
    /// </summary>
    public const string InvitesRead = "invites:read";

    /// <summary>
    /// 创建用户邀请码
    /// </summary>
    public const string InvitesWrite = "invites:write";

    /// <summary>
    /// 获取所有标准 scopes 列表
    /// </summary>
    public static readonly string[] StandardScopes = new[]
    {
        OpenId,
        Email,
        Profile,
        OfflineAccess
    };

    /// <summary>
    /// 获取所有自定义 scopes 列表
    /// </summary>
    public static readonly string[] CustomScopes = new[]
    {
        Roles,
        Apps,
        Consents,
        Tokens,
        SettingsRead,
        SettingsWrite,
        InvitesRead,
        InvitesWrite
    };

    /// <summary>
    /// 获取所有 scopes 列表
    /// </summary>
    public static readonly string[] AllScopes = StandardScopes.Concat(CustomScopes).ToArray();

    /// <summary>
    /// 检查 scope 是否为标准 scope
    /// </summary>
    public static bool IsStandardScope(string scope)
    {
        return StandardScopes.Contains(scope);
    }

    /// <summary>
    /// 检查 scope 是否为自定义 scope
    /// </summary>
    public static bool IsCustomScope(string scope)
    {
        return CustomScopes.Contains(scope);
    }

    /// <summary>
    /// 获取 scope 的描述信息
    /// </summary>
    public static string GetScopeDescription(string scope)
    {
        return scope switch
        {
            OpenId => "允许使用 OpenID Connect 身份验证",
            Email => "访问您的邮箱地址",
            Profile => "访问您的基本信息（用户名、头像等）",
            OfflineAccess => "在您离线时保持访问权限",
            Roles => "访问您的角色信息",
            Apps => "访问您创建的 OAuth 应用列表",
            Consents => "访问您的 OAuth 授权历史",
            Tokens => "访问您的活跃令牌列表",
            SettingsRead => "读取您的设置信息",
            SettingsWrite => "修改您的设置信息",
            InvitesRead => "读取您的邀请码",
            InvitesWrite => "创建新的邀请码",
            _ => "未知权限"
        };
    }
}