namespace backend.Controllers.OAuth;

using backend.Database;
using backend.Database.Entities;
using backend.Services;
using backend.Types.Requests;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Collections.Immutable;
using System.Security.Claims;
using System.Text.Json;

/// <summary>
/// OAuth 2.0 授权端点控制器
/// 路由: /oauth2/*
/// </summary>
[ApiController]
[Route("oauth2")]
public class OAuth2Controller : ControllerBase
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly AppDbContext _db;
    private readonly ILogger<OAuth2Controller> _logger;

    public OAuth2Controller(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictAuthorizationManager authorizationManager,
        IOpenIddictScopeManager scopeManager,
        AppDbContext db,
        ILogger<OAuth2Controller> logger)
    {
        _applicationManager = applicationManager;
        _authorizationManager = authorizationManager;
        _scopeManager = scopeManager;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 获取授权页面数据
    /// GET /api/v1/oauth2/authorize
    /// 需要 Cookie Session 认证
    /// </summary>
    [HttpGet("authorize")]
    [Authorize(Policy = "CookieOnly")]
    [ProducesResponseType<OAuth2AuthorizeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<OAuth2ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest();

        // 获取应用信息（直接用 request.ClientId，中间件保证了它一定存在且有效）
        var application = await _applicationManager.FindByClientIdAsync(request.ClientId);

        // 4. 获取当前用户信息
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return Unauthorized();
        }

        // 5. 验证并格式化 scope
        var requestedScopes = string.IsNullOrEmpty(request.Scope)
            ? new[] { OAuthScopes.Profile }
            : request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var validScopes = new List<string>();
        var scopeDescriptions = new Dictionary<string, string>();

        foreach (var scopeName in requestedScopes)
        {
            var scope = await _scopeManager.FindByNameAsync(scopeName);
            if (scope != null)
            {
                validScopes.Add(scopeName);
                scopeDescriptions[scopeName] = OAuthScopes.GetScopeDescription(scopeName);
            }
        }

        // 6. 获取应用创建者信息
        var properties = await _applicationManager.GetPropertiesAsync(application);
        var creatorId = properties.TryGetValue("user_id", out var creatorIdValue)
            ? creatorIdValue.GetString()
            : null;

        string? creatorUsername = null;
        if (creatorId != null && int.TryParse(creatorId, out var creatorIntId))
        {
            creatorUsername = await _db.Users
                .Where(u => u.Id == creatorIntId)
                .Select(u => u.Username)
                .FirstOrDefaultAsync();
        }

        // 7. 返回授权页面数据
        var response = new OAuth2AuthorizeResponse
        {
            ClientId = request.ClientId,
            AppName = await _applicationManager.GetDisplayNameAsync(application) ?? request.ClientId,
            RedirectUri = request.RedirectUri,
            Scopes = validScopes,
            ScopeDescriptions = scopeDescriptions,
            State = request.State,
            UserId = userId,
            AppCreator = creatorUsername
        };

        return Ok(response);
    }

    /// <summary>
    /// 用户确认/拒绝授权
    /// POST /api/v1/oauth2/approve
    /// 需要 Cookie Session 认证
    /// </summary>
    [HttpPost("approve")]
    [Authorize(Policy = "CookieOnly")]
    [ProducesResponseType<OAuth2ApproveResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Approve()
    {
        // 1. 获取 OpenIddict 自动从上下文（URL 或 Body）解析的请求
        // 注意：前端发起 POST 时，必须带上原始的 QueryString（包含 client_id, response_type 等）
        var request = HttpContext.GetOpenIddictServerRequest();

        // 2. 获取当前用户
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.IsBanned)
        {
            return Unauthorized();
        }

        // 3. 验证 Client（实际上能进到这里说明 client_id 基本合法，但为了获取实体对象可以复用逻辑）
        var application = await _applicationManager.FindByClientIdAsync(request.ClientId);

        // 4. 用户同意授权后的 Scopes 处理
        // 建议直接使用 request.GetScopes()，这是中间件解析后的标准集合
        var requestedScopes = request.GetScopes();

        // 5. 创建 ClaimsPrincipal
        var claims = new List<Claim>
        {
            new Claim(OpenIddictConstants.Claims.Subject, userId.ToString()),
            new Claim(OpenIddictConstants.Claims.Email, user.Email ?? ""),
            new Claim(OpenIddictConstants.Claims.Name, user.Username ?? ""),
            // 使用标准声明名，例如 Roles
            new Claim(OpenIddictConstants.Claims.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // 设置 Scopes 与目的地
        principal.SetScopes(requestedScopes);
        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim, principal));
        }

        // 6. 自动关联或创建授权记录 (可选，取决于你的 Consent 策略)
        var appId = await _applicationManager.GetIdAsync(application);
        var authorization = await CreateAuthorizationAsync(userId, appId!, requestedScopes.ToArray());
        principal.SetAuthorizationId(await _authorizationManager.GetIdAsync(authorization));

        _logger.LogInformation("User {UserId} approved {ClientId}喵~", userId, request.ClientId);

        // 7. 关键修正：返回 SignIn 结果
        // 在 Passthrough 模式下，SignIn 会生成一个包含 authorization_code 的 302 重定向
        // 如果你的 Vue 前端使用 axios 调用，它会尝试跟随这个重定向，导致跨域或数据丢失。
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// 令牌端点
    /// POST /api/v1/oauth2/token
    /// 使用 client_credentials 认证
    /// </summary>
    [HttpPost("token")]
    [EnableRateLimiting("TokenEndpoint")]
    [Produces("application/json")]
    [ProducesResponseType<OAuth2TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<OAuth2ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<OAuth2ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token([FromForm] OAuth2TokenRequest request)
    {
        // 验证客户端
        var application = await _applicationManager.FindByClientIdAsync(request.ClientId);
        if (application == null)
        {
            return Unauthorized(new OAuth2ErrorResponse
            {
                Error = "invalid_client",
                ErrorDescription = "Client not found"
            });
        }

        // 验证客户端密钥
        if (!await _applicationManager.ValidateClientSecretAsync(application, request.ClientSecret))
        {
            return Unauthorized(new OAuth2ErrorResponse
            {
                Error = "invalid_client",
                ErrorDescription = "Invalid client secret"
            });
        }

        // 根据 grant_type 处理
        return request.GrantType switch
        {
            "authorization_code" => await HandleAuthorizationCodeGrant(request, application),
            "refresh_token" => await HandleRefreshTokenGrant(request, application),
            _ => BadRequest(new OAuth2ErrorResponse
            {
                Error = "unsupported_grant_type",
                ErrorDescription = $"Grant type '{request.GrantType}' is not supported"
            })
        };
    }

    /// <summary>
    /// 撤销令牌端点
    /// POST /api/v1/oauth2/revoke
    /// </summary>
    [HttpPost("revoke")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OAuth2ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke([FromForm] OAuth2RevokeRequest request)
    {
        // 验证客户端
        var application = await _applicationManager.FindByClientIdAsync(request.ClientId);
        if (application == null)
        {
            return Unauthorized(new OAuth2ErrorResponse
            {
                Error = "invalid_client",
                ErrorDescription = "Client not found"
            });
        }

        // 验证客户端密钥
        if (!await _applicationManager.ValidateClientSecretAsync(application, request.ClientSecret))
        {
            return Unauthorized(new OAuth2ErrorResponse
            {
                Error = "invalid_client",
                ErrorDescription = "Invalid client secret"
            });
        }

        // 撤销令牌（即使令牌无效也返回成功）
        _logger.LogInformation("Token revocation requested by client {ClientId}", request.ClientId);

        return Ok();
    }

    #region Private Methods

    private IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
    {
        yield return OpenIddictConstants.Destinations.AccessToken;

        if (claim.Type is OpenIddictConstants.Claims.Name or OpenIddictConstants.Claims.Email)
        {
            yield return OpenIddictConstants.Destinations.IdentityToken;
        }
    }

    private async Task<object> CreateAuthorizationAsync(int userId, string applicationId, string[] scopes)
    {
        // 查找现有授权
        await foreach (var auth in _authorizationManager.FindAsync(
                           subject: userId.ToString(),
                           client: applicationId,
                           status: OpenIddictConstants.Statuses.Valid,
                           type: OpenIddictConstants.AuthorizationTypes.Permanent,
                           scopes: scopes.ToImmutableArray()))
        {
            return auth;
        }

        // 创建新授权
        var user = await _db.Users.FindAsync(userId);
        if (user == null) throw new InvalidOperationException("User not found");

        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = applicationId,
            CreationDate = DateTimeOffset.UtcNow,
            Subject = userId.ToString(),
            Type = OpenIddictConstants.AuthorizationTypes.Permanent
        };

        return await _authorizationManager.CreateAsync(descriptor);
    }

    private async Task<IActionResult> HandleAuthorizationCodeGrant(OAuth2TokenRequest request, object application)
    {
        // 验证必填参数
        if (string.IsNullOrEmpty(request.Code) || string.IsNullOrEmpty(request.RedirectUri))
        {
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "invalid_request",
                ErrorDescription = "code and redirect_uri are required for authorization_code grant"
            });
        }

        // 使用 OpenIddict 验证授权码
        var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (authResult?.Principal == null)
        {
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "invalid_grant",
                ErrorDescription = "Invalid or expired authorization code"
            });
        }

        // 验证客户端 ID 匹配
        var clientId = authResult.Principal.FindFirst(OpenIddictConstants.Claims.ClientId)?.Value;
        if (clientId != request.ClientId)
        {
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "invalid_grant",
                ErrorDescription = "Client ID mismatch"
            });
        }

        // 获取用户信息
        var subject = authResult.Principal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        if (string.IsNullOrEmpty(subject) || !int.TryParse(subject, out var userId))
        {
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "invalid_grant",
                ErrorDescription = "Invalid authorization"
            });
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.IsBanned)
        {
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "invalid_grant",
                ErrorDescription = "User not found or banned"
            });
        }

        // 设置 claim 目标
        foreach (var claim in authResult.Principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim, authResult.Principal));
        }

        _logger.LogInformation("Access token issued for user {UserId} to client {ClientId}",
            userId, request.ClientId);

        // 返回令牌
        return SignIn(authResult.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleRefreshTokenGrant(OAuth2TokenRequest request, object application)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "invalid_request",
                ErrorDescription = "refresh_token is required for refresh_token grant"
            });
        }

        // 使用 OpenIddict 验证刷新令牌
        var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (authResult?.Principal == null)
        {
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "invalid_grant",
                ErrorDescription = "Invalid or expired refresh token"
            });
        }

        // 获取用户信息
        var subject = authResult.Principal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        if (string.IsNullOrEmpty(subject) || !int.TryParse(subject, out var userId))
        {
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "invalid_grant",
                ErrorDescription = "Invalid refresh token"
            });
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.IsBanned)
        {
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "invalid_grant",
                ErrorDescription = "User not found or banned"
            });
        }

        // 设置 claim 目标
        foreach (var claim in authResult.Principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim, authResult.Principal));
        }

        _logger.LogInformation("Token refreshed for user {UserId} to client {ClientId}",
            userId, request.ClientId);

        // 返回新令牌
        return SignIn(authResult.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    #endregion
}