using backend;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

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
            var clientType = await _applicationManager.GetClientTypeAsync(app);
            var applicationType = await _applicationManager.GetApplicationTypeAsync(app);
            var consentType = await _applicationManager.GetConsentTypeAsync(app);

            // Get application properties to check owner
            var properties = await _applicationManager.GetPropertiesAsync(app);
            JsonElement? userIdElement = properties.TryGetValue("user_id", out var userIdValue) ? userIdValue : null;
            var userId = userIdElement?.GetString();

            // Only return applications created by current user
            if (userId == currentUserId)
            {
                apps.Add(new OAuthAppResponse
                {
                    Id = appId ?? string.Empty,
                    ClientId = clientId ?? string.Empty,
                    DisplayName = displayName ?? string.Empty,
                    UserId = userId,
                    ClientType = clientType ?? "confidential",
                    ApplicationType = applicationType ?? "web",
                    ConsentType = consentType ?? "explicit"
                });
            }
        }

        return Ok(apps);
    }

    [HttpPost]
    [ProducesResponseType<CreateAppResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateAppResponse>> Create([FromBody] CreateAppRequest request)
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        // Generate a random client ID with prefix "sb_app_"
        var clientId = "sb_app_" + Utils.GenerateRandomSecret(16);

        var clientSecret = Utils.GenerateRandomSecret(48);

        var clientType = request.ClientType?.ToLowerInvariant() switch
        {
            "public" => ClientTypes.Public,
            "confidential" => ClientTypes.Confidential,
            _ => ClientTypes.Confidential
        };

        var applicationType = request.ApplicationType?.ToLowerInvariant() switch
        {
            "web" => ApplicationTypes.Web,
            "native" => ApplicationTypes.Native,
            _ => ApplicationTypes.Web
        };

        var consentType = request.ConsentType?.ToLowerInvariant() switch
        {
            "implicit" => ConsentTypes.Implicit,
            "explicit" => ConsentTypes.Explicit,
            "external" => ConsentTypes.External,
            "systematic" => ConsentTypes.Systematic,
            _ => ConsentTypes.Explicit
        };

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientType = clientType,
            DisplayName = request.DisplayName,
            ApplicationType = applicationType,
            ConsentType = consentType,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile
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
            var app = await _applicationManager.CreateAsync(descriptor);
            var appId = await _applicationManager.GetIdAsync(app);
            _logger.LogInformation("OAuth app {ClientId} created by user {UserId}", clientId, currentUserId);

            var response = new CreateAppResponse(
                Id: appId ?? string.Empty,
                ClientId: clientId,
                ClientSecret: clientSecret,
                DisplayName: request.DisplayName
            );

            return CreatedAtAction(nameof(GetMyApps), response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create OAuth app {ClientId}", clientId);
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

        // Check permission: only the application owner can delete
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

    [HttpGet("{id}")]
    [ProducesResponseType<OAuthAppDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OAuthAppDetailResponse>> GetAppById(string id)
    {
        var app = await _applicationManager.FindByIdAsync(id);
        if (app is null)
        {
            return NotFound();
        }

        // Check permission: only the application owner can view details
        var properties = await _applicationManager.GetPropertiesAsync(app);
        var ownerUserId = properties.TryGetValue("user_id", out var userIdValue) ? userIdValue.GetString() : null;
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (ownerUserId != currentUserId)
        {
            _logger.LogWarning("User {CurrentUserId} attempted to view OAuth app {AppId} owned by {OwnerUserId}", currentUserId, id, ownerUserId);
            return Forbid();
        }

        var clientId = await _applicationManager.GetClientIdAsync(app);
        var displayName = await _applicationManager.GetDisplayNameAsync(app);
        var appId = await _applicationManager.GetIdAsync(app);
        var redirectUris = await _applicationManager.GetRedirectUrisAsync(app);
        var clientType = await _applicationManager.GetClientTypeAsync(app);
        var applicationType = await _applicationManager.GetApplicationTypeAsync(app);
        var consentType = await _applicationManager.GetConsentTypeAsync(app);

        var response = new OAuthAppDetailResponse
        {
            Id = appId ?? string.Empty,
            ClientId = clientId ?? string.Empty,
            DisplayName = displayName ?? string.Empty,
            UserId = ownerUserId,
            RedirectUris = redirectUris.Select(u => u.ToString()).ToList(),
            ClientType = clientType ?? "confidential",
            ApplicationType = applicationType ?? "web",
            ConsentType = consentType ?? "explicit"
        };

        return Ok(response);
    }

    [HttpPut("{id}")]
    [ProducesResponseType<OAuthAppResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OAuthAppResponse>> UpdateApp(string id, [FromBody] UpdateAppRequest request)
    {
        var app = await _applicationManager.FindByIdAsync(id);
        if (app is null)
        {
            return NotFound();
        }

        // Check permission: only the application owner can update
        var properties = await _applicationManager.GetPropertiesAsync(app);
        var ownerUserId = properties.TryGetValue("user_id", out var userIdValue) ? userIdValue.GetString() : null;
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (ownerUserId != currentUserId)
        {
            _logger.LogWarning("User {CurrentUserId} attempted to update OAuth app {AppId} owned by {OwnerUserId}", currentUserId, id, ownerUserId);
            return Forbid();
        }

        // Populate descriptor from existing application to preserve all properties
        var descriptor = new OpenIddictApplicationDescriptor();
        await _applicationManager.PopulateAsync(descriptor, app);

        // Update the fields that are provided in the request
        descriptor.DisplayName = request.DisplayName;

        if (request.RedirectUris is not null)
        {
            descriptor.RedirectUris.Clear();
            foreach (var uri in request.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }
        }

        if (request.ClientType is not null)
        {
            descriptor.ClientType = request.ClientType.ToLowerInvariant() switch
            {
                "public" => ClientTypes.Public,
                "confidential" => ClientTypes.Confidential,
                _ => ClientTypes.Confidential
            };
        }

        if (request.ApplicationType is not null)
        {
            descriptor.ApplicationType = request.ApplicationType.ToLowerInvariant() switch
            {
                "web" => ApplicationTypes.Web,
                "native" => ApplicationTypes.Native,
                _ => ApplicationTypes.Web
            };
        }

        if (request.ConsentType is not null)
        {
            descriptor.ConsentType = request.ConsentType.ToLowerInvariant() switch
            {
                "implicit" => ConsentTypes.Implicit,
                "explicit" => ConsentTypes.Explicit,
                "external" => ConsentTypes.External,
                "systematic" => ConsentTypes.Systematic,
                _ => ConsentTypes.Explicit
            };
        }

        try
        {
            await _applicationManager.UpdateAsync(app, descriptor);
            _logger.LogInformation("OAuth app {AppId} updated by user {UserId}", id, currentUserId);

            var clientId = await _applicationManager.GetClientIdAsync(app);
            var displayName = await _applicationManager.GetDisplayNameAsync(app);
            var appId = await _applicationManager.GetIdAsync(app);
            var clientType = await _applicationManager.GetClientTypeAsync(app);
            var applicationType = await _applicationManager.GetApplicationTypeAsync(app);
            var consentType = await _applicationManager.GetConsentTypeAsync(app);

            var response = new OAuthAppResponse
            {
                Id = appId ?? string.Empty,
                ClientId = clientId ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                UserId = ownerUserId,
                ClientType = clientType ?? "confidential",
                ApplicationType = applicationType ?? "web",
                ConsentType = consentType ?? "explicit"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update OAuth app {AppId}", id);
            return BadRequest(new { message = "Could not update application" });
        }
    }

    [HttpPatch("{id}")]
    [ProducesResponseType<OAuthAppResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OAuthAppResponse>> PatchApp(string id, [FromBody] PatchAppRequest request)
    {
        var app = await _applicationManager.FindByIdAsync(id);
        if (app is null)
        {
            return NotFound();
        }

        // Check permission: only the application owner can update
        var properties = await _applicationManager.GetPropertiesAsync(app);
        var ownerUserId = properties.TryGetValue("user_id", out var userIdValue) ? userIdValue.GetString() : null;
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (ownerUserId != currentUserId)
        {
            _logger.LogWarning("User {CurrentUserId} attempted to patch OAuth app {AppId} owned by {OwnerUserId}", currentUserId, id, ownerUserId);
            return Forbid();
        }

        // Populate descriptor from existing application to preserve all properties
        var descriptor = new OpenIddictApplicationDescriptor();
        await _applicationManager.PopulateAsync(descriptor, app);

        // Update only the fields that are provided in the request
        if (request.DisplayName is not null)
        {
            descriptor.DisplayName = request.DisplayName;
        }

        if (request.RedirectUris is not null)
        {
            descriptor.RedirectUris.Clear();
            foreach (var uri in request.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }
        }

        if (request.ClientType is not null)
        {
            descriptor.ClientType = request.ClientType.ToLowerInvariant() switch
            {
                "public" => ClientTypes.Public,
                "confidential" => ClientTypes.Confidential,
                _ => ClientTypes.Confidential
            };
        }

        if (request.ApplicationType is not null)
        {
            descriptor.ApplicationType = request.ApplicationType.ToLowerInvariant() switch
            {
                "web" => ApplicationTypes.Web,
                "native" => ApplicationTypes.Native,
                _ => ApplicationTypes.Web
            };
        }

        if (request.ConsentType is not null)
        {
            descriptor.ConsentType = request.ConsentType.ToLowerInvariant() switch
            {
                "implicit" => ConsentTypes.Implicit,
                "explicit" => ConsentTypes.Explicit,
                "external" => ConsentTypes.External,
                "systematic" => ConsentTypes.Systematic,
                _ => ConsentTypes.Explicit
            };
        }

        try
        {
            await _applicationManager.UpdateAsync(app, descriptor);
            _logger.LogInformation("OAuth app {AppId} patched by user {UserId}", id, currentUserId);

            var clientId = await _applicationManager.GetClientIdAsync(app);
            var displayName = await _applicationManager.GetDisplayNameAsync(app);
            var appId = await _applicationManager.GetIdAsync(app);
            var clientType = await _applicationManager.GetClientTypeAsync(app);
            var applicationType = await _applicationManager.GetApplicationTypeAsync(app);
            var consentType = await _applicationManager.GetConsentTypeAsync(app);

            var response = new OAuthAppResponse
            {
                Id = appId ?? string.Empty,
                ClientId = clientId ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                UserId = ownerUserId,
                ClientType = clientType ?? "confidential",
                ApplicationType = applicationType ?? "web",
                ConsentType = consentType ?? "explicit"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to patch OAuth app {AppId}", id);
            return BadRequest(new { message = "Could not update application" });
        }
    }

    [HttpPost("{id}/secret")]
    [ProducesResponseType<NewSecretResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<NewSecretResponse>> RegenerateSecret(string id)
    {
        var app = await _applicationManager.FindByIdAsync(id);
        if (app is null)
        {
            return NotFound();
        }

        // Check permission: only the application owner can regenerate secret
        var properties = await _applicationManager.GetPropertiesAsync(app);
        var ownerUserId = properties.TryGetValue("user_id", out var userIdValue) ? userIdValue.GetString() : null;
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (ownerUserId != currentUserId)
        {
            _logger.LogWarning("User {CurrentUserId} attempted to regenerate secret for OAuth app {AppId} owned by {OwnerUserId}", currentUserId, id, ownerUserId);
            return Forbid();
        }

        var newSecret = Utils.GenerateRandomSecret(48);

        // Populate descriptor from existing application to preserve all properties
        var descriptor = new OpenIddictApplicationDescriptor();
        await _applicationManager.PopulateAsync(descriptor, app);

        // Update only the client secret
        descriptor.ClientSecret = newSecret;

        try
        {
            await _applicationManager.UpdateAsync(app, descriptor);
            _logger.LogInformation("Secret regenerated for OAuth app {AppId} by user {UserId}", id, currentUserId);

            return Ok(new NewSecretResponse(newSecret));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to regenerate secret for OAuth app {AppId}", id);
            return BadRequest(new { message = "Could not regenerate secret" });
        }
    }
}