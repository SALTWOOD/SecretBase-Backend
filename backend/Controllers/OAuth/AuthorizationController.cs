using backend.Controllers.OAuth;
using backend.Database;
using backend.Database.Entities;
using backend.Services;
using backend;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace backend.Controllers.OAuth;

[ApiController]
public class AuthorizationController : BaseApiController
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ILogger<AuthorizationController> _logger;

    public AuthorizationController(
        BaseServices deps,
        IOpenIddictApplicationManager applicationManager,
        ILogger<AuthorizationController> logger) : base(deps)
    {
        _applicationManager = applicationManager;
        _logger = logger;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return BadRequest(new { error = "invalid_request", description = "The OIDC request is missing." });
        }

        if (User.Identity is not { IsAuthenticated: true })
        {
            return Challenge();
        }

        var application = await _applicationManager.FindByClientIdAsync(request.ClientId ?? string.Empty);
        if (application is null)
        {
            return BadRequest(new { error = "invalid_client", description = "Client not found." });
        }

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id.ToString() == subject);
        if (user is null)
        {
            return BadRequest(new { error = "invalid_user", description = "User not found." });
        }

        // Validate state parameter for CSRF protection
        var state = request.State;
        if (string.IsNullOrEmpty(state))
        {
            _logger.LogWarning("Authorization request missing state parameter from client {ClientId}", request.ClientId);
            return BadRequest(new { error = "invalid_request", description = "State parameter is required for CSRF protection." });
        }

        var claims = new List<Claim>
        {
            new Claim(OpenIddictConstants.Claims.Subject, subject),
            new Claim(OpenIddictConstants.Claims.Email, User.FindFirstValue(ClaimTypes.Email) ?? string.Empty),
            new Claim(OpenIddictConstants.Claims.Name, User.FindFirstValue(ClaimTypes.Name) ?? string.Empty),
            new Claim(OAuthScopes.Roles, user.Role.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        // Validate requested scopes
        var requestedScopes = request.GetScopes();
        var validScopes = new HashSet<string>(OAuthScopes.AllScopes);
        var invalidScopes = requestedScopes.Where(s => !validScopes.Contains(s)).ToList();

        if (invalidScopes.Any())
        {
            _logger.LogWarning("Invalid scopes requested by client {ClientId}: {Scopes}", request.ClientId, string.Join(", ", invalidScopes));
            return BadRequest(new { error = "invalid_scope", description = $"Invalid scopes: {string.Join(", ", invalidScopes)}" });
        }

        claimsPrincipal.SetScopes(requestedScopes);

        _logger.LogInformation("User {Subject} authorized {ClientId}", subject, request.ClientId);

        return SignIn(claimsPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        yield return OpenIddictConstants.Destinations.AccessToken;

        if (claim.Type is OpenIddictConstants.Claims.Name
                       or OpenIddictConstants.Claims.PreferredUsername
                       or OpenIddictConstants.Claims.Email)
        {
            yield return OpenIddictConstants.Destinations.IdentityToken;
        }
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("TokenEndpoint")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OpenIddictTokenResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return BadRequest(new { error = "invalid_request" });
        }

        if (request.IsAuthorizationCodeGrantType())
        {
            var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (authResult.Principal is null)
            {
                return BadRequest(new { error = "invalid_grant" });
            }

            _logger.LogDebug("Token issued for client {ClientId}", request.ClientId);

            foreach (var claim in authResult.Principal.Claims)
            {
                // 默认情况下，OpenIddict 不会将所有 Claim 放入 Token
                // 我们需要显式指定它们去往何处
                claim.SetDestinations(GetDestinations(claim));
            }

            return SignIn(authResult.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsRefreshTokenGrantType())
        {
            var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (authResult.Principal is null)
            {
                return BadRequest(new { error = "invalid_grant" });
            }

            _logger.LogDebug("Token refreshed for client {ClientId}", request.ClientId);
            return SignIn(authResult.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest(new { error = "unsupported_grant_type" });
    }

    [HttpGet("~/connect/userinfo")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UserInfo()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return BadRequest(new { error = "invalid_request" });
        }

        var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (authResult.Principal is null)
        {
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var subject = authResult.Principal.FindFirstValue(OpenIddictConstants.Claims.Subject);
        if (string.IsNullOrEmpty(subject))
        {
            return BadRequest(new { error = "invalid_token" });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id.ToString() == subject);
        if (user is null)
        {
            return BadRequest(new { error = "invalid_token" });
        }

        var claims = new Dictionary<string, object>();
        claims[OpenIddictConstants.Claims.Subject] = user.Id.ToString();

        // Email scope
        if (authResult.Principal.HasScope(OpenIddictConstants.Permissions.Scopes.Email))
        {
            claims[OpenIddictConstants.Claims.Email] = user.Email ?? string.Empty;
            claims[OpenIddictConstants.Claims.EmailVerified] = true;
        }

        // Profile scope
        if (authResult.Principal.HasScope(OpenIddictConstants.Permissions.Scopes.Profile))
        {
            claims[OpenIddictConstants.Claims.Name] = user.Username ?? string.Empty;
        }

        // Roles scope
        if (authResult.Principal.HasScope("roles"))
        {
            claims["roles"] = new[] { user.Role.ToString() };
        }

        return Ok(claims);
    }

    [HttpPost("~/connect/revoke")]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Revoke()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return BadRequest(new { error = "invalid_request" });
        }

        var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (authResult.Principal is null)
        {
            return BadRequest(new { error = "invalid_token" });
        }

        _logger.LogInformation("Token revoked for client {ClientId}", request.ClientId);
        return Ok(new { });
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return BadRequest(new { error = "invalid_request" });
        }

        // Sign out the current user
        await HttpContext.SignOutAsync();

        // If there's a post_logout_redirect_uri, redirect to it
        var postLogoutRedirectUri = request.PostLogoutRedirectUri;
        if (!string.IsNullOrEmpty(postLogoutRedirectUri))
        {
            return Redirect(postLogoutRedirectUri);
        }

        return Ok(new { message = "Logged out successfully" });
    }
}