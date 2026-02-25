namespace backend.Filters;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using System.Text.Encodings.Web;

/// <summary>
/// OAuth Bearer Token 认证处理器
/// 用于验证第三方应用通过 OAuth 2.0 获取的 access_token
/// </summary>
public class OAuthBearerAuthenticator : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "OAuthBearer";

    public OAuthBearerAuthenticator(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. 从 Authorization header 提取 Bearer token
        string? authorizationHeader = Request.Headers.Authorization;
        
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return AuthenticateResult.NoResult();
        }

        if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorizationHeader.Substring("Bearer ".Length).Trim();
        
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.Fail("Invalid bearer token format");
        }

        // 2. 使用 OpenIddict 验证 token
        var authenticateResult = await Context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (authenticateResult?.Principal == null)
        {
            return AuthenticateResult.Fail("Invalid or expired access token");
        }

        // 3. 检查是否是 access token 类型
        var tokenType = authenticateResult.Principal.FindFirst("token_type")?.Value;
        if (tokenType != "access_token")
        {
            return AuthenticateResult.Fail("Token is not an access token");
        }

        // 4. 获取用户信息并构建 ClaimsPrincipal
        var subject = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? authenticateResult.Principal.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(subject))
        {
            return AuthenticateResult.Fail("Token does not contain subject claim");
        }

        // 5. 提取 scopes
        var scopes = authenticateResult.Principal.FindAll("scope").Select(c => c.Value).ToList();

        // 6. 构建 OAuth 认证的 ClaimsIdentity
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, subject),
            new Claim("auth_type", "oauth")
        };

        // 添加 scope claims
        foreach (var scope in scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        // 复制其他有用的 claims
        var email = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value
            ?? authenticateResult.Principal.FindFirst("email")?.Value;
        if (!string.IsNullOrEmpty(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        var name = authenticateResult.Principal.FindFirst(ClaimTypes.Name)?.Value
            ?? authenticateResult.Principal.FindFirst("name")?.Value;
        if (!string.IsNullOrEmpty(name))
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);

        Logger.LogDebug("OAuth Bearer token validated for user {Subject} with scopes {Scopes}", 
            subject, string.Join(", ", scopes));

        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
