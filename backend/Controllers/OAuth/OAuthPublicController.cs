namespace backend.Controllers.OAuth;

using backend;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

[ApiController]
[Route("oauth/public")]
public class OAuthPublicController : ControllerBase
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ILogger<OAuthPublicController> _logger;

    public OAuthPublicController(IOpenIddictApplicationManager applicationManager, ILogger<OAuthPublicController> logger)
    {
        _applicationManager = applicationManager;
        _logger = logger;
    }

    [HttpGet("app-info")]
    [Authorize]
    [ProducesResponseType<OAuthAppResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppInfo(string clientId)
    {
        // Use Manager to query application entity
        var application = await _applicationManager.FindByClientIdAsync(clientId);

        if (application == null)
        {
            _logger.LogWarning("Public info request for non-existent client: {ClientId}", clientId);
            return NotFound(new MessageResponse { Message = "Application not found" });
        }

        // Map to DTO and return to frontend
        // Only expose safe public fields, never expose ClientSecret!
        var response = new OAuthAppResponse
        {
            ClientId = (await _applicationManager.GetClientIdAsync(application)).ThrowIfNull(),
            DisplayName = (await _applicationManager.GetDisplayNameAsync(application)).ThrowIfNull(),
            ClientType = await _applicationManager.GetClientTypeAsync(application) ?? "confidential",
            ApplicationType = await _applicationManager.GetApplicationTypeAsync(application) ?? "web",
            ConsentType = await _applicationManager.GetConsentTypeAsync(application) ?? "explicit"
        };

        return Ok(response);
    }
}