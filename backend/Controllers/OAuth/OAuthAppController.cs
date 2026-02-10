using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using System.Text.Json;

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
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        await foreach (var app in _applicationManager.ListAsync())
        {
            var clientId = await _applicationManager.GetClientIdAsync(app);
            var displayName = await _applicationManager.GetDisplayNameAsync(app);
            var appId = await _applicationManager.GetIdAsync(app);

            // 获取应用的属性来检查所有者
            var properties = await _applicationManager.GetPropertiesAsync(app);
            JsonElement? userIdElement = properties.TryGetValue("user_id", out var userIdValue) ? userIdValue : null;
            var userId = userIdElement?.GetString();

            // 只返回当前用户创建的应用
            if (userId == currentUserId)
            {
                apps.Add(new OAuthAppResponse
                {
                    Id = appId ?? string.Empty,
                    ClientId = clientId ?? string.Empty,
                    DisplayName = displayName ?? string.Empty,
                    UserId = userId
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
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

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
            Properties =
            {
                ["user_id"] = JsonSerializer.SerializeToElement(currentUserId)
            }
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
            _logger.LogInformation("OAuth app {ClientId} created by user {UserId}", request.ClientId, currentUserId);

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(string id)
    {
        var app = await _applicationManager.FindByIdAsync(id);
        if (app is null)
        {
            return NotFound();
        }

        // 检查权限：只有应用的所有者才能删除
        var properties = await _applicationManager.GetPropertiesAsync(app);
        var ownerUserId = properties.TryGetValue("user_id", out var userIdValue) ? userIdValue.GetString() : null;
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (ownerUserId != currentUserId)
        {
            _logger.LogWarning("User {CurrentUserId} attempted to delete OAuth app {AppId} owned by {OwnerUserId}", currentUserId, id, ownerUserId);
            return Forbid();
        }

        await _applicationManager.DeleteAsync(app);
        _logger.LogInformation("OAuth app {AppId} deleted by user {UserId}", id, currentUserId);

        return NoContent();
    }
}