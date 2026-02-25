namespace backend.Types.Requests;

using System.ComponentModel.DataAnnotations;

#region 授权请求模型

/// <summary>
/// OAuth2 授权请求参数
/// GET /api/v1/oauth2/authorize
/// </summary>
public class OAuth2AuthorizeRequest
{
    /// <summary>
    /// 响应类型，固定为 "code"
    /// </summary>
    [Required]
    public string ResponseType { get; set; } = "code";

    /// <summary>
    /// 客户端 ID
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 重定向 URI，必须在应用的白名单中
    /// </summary>
    [Required]
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// 请求的权限范围，空格分隔
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// 状态参数，用于防止 CSRF 攻击
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// PKCE code_challenge
    /// </summary>
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// PKCE code_challenge_method
    /// </summary>
    public string? CodeChallengeMethod { get; set; }
}

/// <summary>
/// 授权确认请求
/// POST /api/v1/oauth2/approve
/// </summary>
public class OAuth2ApproveRequest
{
    /// <summary>
    /// 客户端 ID
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 重定向 URI
    /// </summary>
    [Required]
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// 请求的权限范围
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// 是否同意授权
    /// </summary>
    public bool Approved { get; set; }

    /// <summary>
    /// 状态参数
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// PKCE code_challenge
    /// </summary>
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// PKCE code_challenge_method
    /// </summary>
    public string? CodeChallengeMethod { get; set; }
}

#endregion

#region 令牌请求模型

/// <summary>
/// OAuth2 令牌请求
/// POST /api/v1/oauth2/token
/// </summary>
public class OAuth2TokenRequest
{
    /// <summary>
    /// 授权类型: authorization_code 或 refresh_token
    /// </summary>
    [Required]
    public string GrantType { get; set; } = string.Empty;

    /// <summary>
    /// 授权码（authorization_code 流程必填）
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// 重定向 URI（authorization_code 流程必填）
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// 刷新令牌（refresh_token 流程必填）
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// 客户端 ID
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 客户端密钥
    /// </summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// PKCE code_verifier
    /// </summary>
    public string? CodeVerifier { get; set; }
}

/// <summary>
/// OAuth2 令牌撤销请求
/// POST /api/v1/oauth2/revoke
/// </summary>
public class OAuth2RevokeRequest
{
    /// <summary>
    /// 要撤销的令牌
    /// </summary>
    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 令牌类型提示: access_token 或 refresh_token
    /// </summary>
    public string? TokenTypeHint { get; set; }

    /// <summary>
    /// 客户端 ID
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 客户端密钥
    /// </summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;
}

#endregion

#region 响应模型

/// <summary>
/// 授权页面数据响应
/// </summary>
public class OAuth2AuthorizeResponse
{
    /// <summary>
    /// 客户端 ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 应用名称
    /// </summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>
    /// 应用描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 应用主页
    /// </summary>
    public string? Homepage { get; set; }

    /// <summary>
    /// 重定向 URI
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// 请求的权限范围
    /// </summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>
    /// 权限范围描述
    /// </summary>
    public Dictionary<string, string> ScopeDescriptions { get; set; } = new();

    /// <summary>
    /// 状态参数
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// 当前用户 ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 应用创建者用户名
    /// </summary>
    public string? AppCreator { get; set; }
}

/// <summary>
/// 授权确认结果响应
/// </summary>
public class OAuth2ApproveResultResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 重定向 URL（包含 code 或 error）
    /// </summary>
    public string? RedirectUrl { get; set; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 错误描述
    /// </summary>
    public string? ErrorDescription { get; set; }
}

/// <summary>
/// OAuth2 令牌响应
/// </summary>
public class OAuth2TokenResponse
{
    /// <summary>
    /// 访问令牌
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 令牌类型
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// 过期时间（秒）
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// 刷新令牌
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// 权限范围
    /// </summary>
    public string? Scope { get; set; }
}

/// <summary>
/// OAuth2 错误响应
/// </summary>
public class OAuth2ErrorResponse
{
    /// <summary>
    /// 错误代码
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// 错误描述
    /// </summary>
    public string? ErrorDescription { get; set; }
}

#endregion
