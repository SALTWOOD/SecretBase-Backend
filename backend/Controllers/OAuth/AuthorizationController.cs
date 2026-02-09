using backend.Controllers.OAuth;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;

namespace backend.Controllers.OAuth;

[ApiController]
public class AuthorizationController : ControllerBase
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ILogger<AuthorizationController> _logger;

    public AuthorizationController(
        IOpenIddictApplicationManager applicationManager,
        ILogger<AuthorizationController> logger)
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
        var claims = new List<Claim>
        {
            new Claim(OpenIddictConstants.Claims.Subject, subject),
            new Claim(OpenIddictConstants.Claims.Email, User.FindFirstValue(ClaimTypes.Email) ?? string.Empty),
            new Claim(OpenIddictConstants.Claims.Name, User.FindFirstValue(ClaimTypes.Name) ?? string.Empty)
        };

        var claimsIdentity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        claimsPrincipal.SetScopes(request.GetScopes());

        _logger.LogInformation("User {Subject} authorized {ClientId}", subject, request.ClientId);

        return SignIn(claimsPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
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
            return SignIn(authResult.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest(new { error = "unsupported_grant_type" });
    }
}