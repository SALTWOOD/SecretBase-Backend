namespace backend.Controllers.OAuth;

using backend.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

/// <summary>
/// Controller for managing user OAuth consents/authorizations
/// </summary>
[Authorize]
[ApiController]
[Route("oauth/consents")]
[Produces("application/json")]
public class OAuthConsentController : ControllerBase
{
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IOpenIddictTokenManager _tokenManager;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ILogger<OAuthConsentController> _logger;

    public OAuthConsentController(
        IOpenIddictAuthorizationManager authorizationManager,
        IOpenIddictTokenManager tokenManager,
        IOpenIddictApplicationManager applicationManager,
        ILogger<OAuthConsentController> logger)
    {
        _authorizationManager = authorizationManager;
        _tokenManager = tokenManager;
        _applicationManager = applicationManager;
        _logger = logger;
    }

    /// <summary>
    /// Get all OAuth consents for the current user
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<OAuthConsentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OAuthConsentResponse>>> GetMyConsents()
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var consents = new List<OAuthConsentResponse>();

        await foreach (var authorization in _authorizationManager.FindAsync(
            subject: currentUserId,
            status: OpenIddictConstants.Statuses.Valid,
            type: OpenIddictConstants.AuthorizationTypes.Permanent,
            client: null,
            scopes: null))
        {
            var applicationId = await _authorizationManager.GetApplicationIdAsync(authorization);
            if (applicationId is null) continue;

            var application = await _applicationManager.FindByIdAsync(applicationId);
            if (application is null) continue;

            var clientId = await _applicationManager.GetClientIdAsync(application);
            var displayName = await _applicationManager.GetDisplayNameAsync(application);
            var authorizationId = await _authorizationManager.GetIdAsync(authorization);
            var creationDate = await _authorizationManager.GetCreationDateAsync(authorization);

            consents.Add(new OAuthConsentResponse
            {
                Id = authorizationId ?? string.Empty,
                ApplicationId = applicationId ?? string.Empty,
                ClientId = clientId ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                CreatedAt = creationDate.HasValue ? creationDate.Value.DateTime : DateTime.UtcNow
            });
        }

        return Ok(consents);
    }

    /// <summary>
    /// Revoke a specific OAuth consent
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeConsent(string id)
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var authorization = await _authorizationManager.FindByIdAsync(id);
        if (authorization is null)
        {
            return NotFound(new { message = "Consent not found" });
        }

        // Check if this authorization belongs to the current user
        var subject = await _authorizationManager.GetSubjectAsync(authorization);
        if (subject != currentUserId)
        {
            _logger.LogWarning("User {CurrentUserId} attempted to revoke consent {ConsentId} owned by {Subject}", currentUserId, id, subject);
            return Forbid();
        }

        // Revoke the authorization
        await _authorizationManager.TryRevokeAsync(authorization);

        _logger.LogInformation("OAuth consent {ConsentId} revoked by user {UserId}", id, currentUserId);

        return NoContent();
    }

    /// <summary>
    /// Get active tokens for the current user
    /// </summary>
    [HttpGet("tokens")]
    [ProducesResponseType<IEnumerable<OAuthTokenResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OAuthTokenResponse>>> GetMyTokens()
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var tokens = new List<OAuthTokenResponse>();

        await foreach (var token in _tokenManager.FindAsync(
            subject: currentUserId,
            status: OpenIddictConstants.Statuses.Valid,
            client: null,
            type: null))
        {
            var tokenType = await _tokenManager.GetTypeAsync(token);
            var applicationId = await _tokenManager.GetApplicationIdAsync(token);
            var tokenId = await _tokenManager.GetIdAsync(token);
            var expirationDate = await _tokenManager.GetExpirationDateAsync(token);
            var creationDate = await _tokenManager.GetCreationDateAsync(token);

            if (applicationId is null || tokenId is null) continue;

            var application = await _applicationManager.FindByIdAsync(applicationId);
            if (application is null) continue;

            var clientId = await _applicationManager.GetClientIdAsync(application);
            var displayName = await _applicationManager.GetDisplayNameAsync(application);

            tokens.Add(new OAuthTokenResponse
            {
                Id = tokenId,
                ApplicationId = applicationId,
                ClientId = clientId ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                TokenType = tokenType ?? string.Empty,
                ExpiresAt = expirationDate.HasValue ? expirationDate.Value.DateTime : DateTime.UtcNow,
                CreatedAt = creationDate.HasValue ? creationDate.Value.DateTime : DateTime.UtcNow
            });
        }

        return Ok(tokens);
    }

    /// <summary>
    /// Revoke a specific token
    /// </summary>
    [HttpDelete("tokens/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeToken(string id)
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var token = await _tokenManager.FindByIdAsync(id);
        if (token is null)
        {
            return NotFound(new { message = "Token not found" });
        }

        // Check if this token belongs to the current user
        var subject = await _tokenManager.GetSubjectAsync(token);
        if (subject != currentUserId)
        {
            _logger.LogWarning("User {CurrentUserId} attempted to revoke token {TokenId} owned by {Subject}", currentUserId, id, subject);
            return Forbid();
        }

        await _tokenManager.TryRevokeAsync(token);

        _logger.LogInformation("OAuth token {TokenId} revoked by user {UserId}", id, currentUserId);

        return NoContent();
    }
}

#region Response Types

/// <summary>
/// OAuth consent response
/// </summary>
public record OAuthConsentResponse
{
    public string Id { get; init; } = string.Empty;
    public string ApplicationId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// OAuth token response
/// </summary>
public record OAuthTokenResponse
{
    public string Id { get; init; } = string.Empty;
    public string ApplicationId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string TokenType { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

#endregion
