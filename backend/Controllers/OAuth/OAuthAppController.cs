using backend.Controllers.OAuth;
using backend.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace backend.Controllers.OAuth;

[Authorize]
[ApiController]
[Route("oauth/apps")]
[Produces("application/json")]
public class OAuthAppController : ControllerBase
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ILogger<OAuthAppController> _logger;

    public OAuthAppController(
        IOpenIddictApplicationManager applicationManager,
        ILogger<OAuthAppController> logger)
    {
        _applicationManager = applicationManager;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<OAuthAppResponse>))]
    public async Task<ActionResult<IEnumerable<OAuthAppResponse>>> GetMyApps()
    {
        var apps = new List<OAuthAppResponse>();

        await foreach (var app in _applicationManager.ListAsync())
        {
            // 使用模式匹配进行安全转换
            if (app is OpenIddictSqlSugarApplication sugarApp)
            {
                apps.Add(new OAuthAppResponse
                {
                    Id = sugarApp.Id,
                    ClientId = sugarApp.ClientId ?? string.Empty, // 处理可能的 null
                    DisplayName = sugarApp.DisplayName ?? string.Empty
                });
            }
        }

        return Ok(apps);
    }

    [HttpPost]
    [ProducesResponseType<OAuthAppResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OAuthAppResponse>> Create([FromBody] CreateAppRequest request)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = request.ClientId,
            ClientSecret = request.ClientSecret,
            DisplayName = request.DisplayName,
            ApplicationType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile
            },
        };

        if (request.RedirectUris is not null)
        {
            foreach (var uri in request.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }
        }

        try
        {
            await _applicationManager.CreateAsync(descriptor);
            _logger.LogInformation("OAuth app {ClientId} created by user", request.ClientId);

            return CreatedAtAction(nameof(GetMyApps), new OAuthAppResponse { ClientId = request.ClientId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create OAuth app {ClientId}", request.ClientId);
            return BadRequest(new { message = "Could not create application" });
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var app = await _applicationManager.FindByIdAsync(id);
        if (app is null)
        {
            return NotFound();
        }

        await _applicationManager.DeleteAsync(app);
        _logger.LogWarning("OAuth app {AppId} deleted", id);

        return NoContent();
    }
}