namespace backend.Controllers.OAuth;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Shared validator for OAuth application requests
/// </summary>
public static class OAuthAppValidator
{
    private const int MaxDisplayNameLength = 100;
    private const int MaxRedirectUris = 10;

    /// <summary>
    /// Validate display name (required for Create/Update, optional for Patch)
    /// </summary>
    public static List<ValidationResult> ValidateDisplayName(string? displayName, bool isRequired = true)
    {
        var results = new List<ValidationResult>();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            if (isRequired) results.Add(new ValidationResult("DisplayName is required."));
        }
        else if (displayName.Length > MaxDisplayNameLength)
        {
            results.Add(new ValidationResult($"DisplayName cannot exceed {MaxDisplayNameLength} characters."));
        }

        return results;
    }

    /// <summary>
    /// Validate redirect URIs
    /// </summary>
    public static List<ValidationResult> ValidateRedirectUris(List<string>? redirectUris, string? applicationType)
    {
        var results = new List<ValidationResult>();

        if (redirectUris != null && redirectUris.Count > 0)
        {
            if (redirectUris.Count > MaxRedirectUris)
                results.Add(new ValidationResult($"Cannot have more than {MaxRedirectUris} redirect URIs."));

            foreach (var uri in redirectUris)
            {
                if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
                {
                    results.Add(new ValidationResult($"Invalid redirect URI: {uri}"));
                    continue;
                }

                // Only allow http/https schemes
                if (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps)
                    results.Add(new ValidationResult($"Redirect URI must use http or https scheme: {uri}"));

                // For non-native apps, require HTTPS in production
                if (applicationType?.ToLowerInvariant() != "native" && parsedUri.Scheme == Uri.UriSchemeHttp)
                    results.Add(new ValidationResult($"Redirect URI must use HTTPS for web applications: {uri}"));
            }
        }

        return results;
    }

    /// <summary>
    /// Validate client type
    /// </summary>
    public static List<ValidationResult> ValidateClientType(string? clientType)
    {
        var results = new List<ValidationResult>();

        if (clientType != null)
        {
            var clientTypeLower = clientType.ToLowerInvariant();
            if (clientTypeLower != "public" && clientTypeLower != "confidential")
                results.Add(new ValidationResult("ClientType must be 'public' or 'confidential'."));
        }

        return results;
    }

    /// <summary>
    /// Validate application type
    /// </summary>
    public static List<ValidationResult> ValidateApplicationType(string? applicationType)
    {
        var results = new List<ValidationResult>();

        if (applicationType != null)
        {
            var appTypeLower = applicationType.ToLowerInvariant();
            if (appTypeLower != "web" && appTypeLower != "native")
                results.Add(new ValidationResult("ApplicationType must be 'web' or 'native'."));
        }

        return results;
    }
}

/// <summary>
/// Request to create a new OAuth application
/// </summary>
/// <param name="DisplayName">Display name for the application</param>
/// <param name="RedirectUris">List of allowed redirect URIs</param>
/// <param name="ClientType">Client type: "public" or "confidential" (default: "confidential")</param>
/// <param name="ApplicationType">Application type: "web" or "native" (default: "web")</param>
public record CreateAppRequest(
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    string DisplayName,
    List<string>? RedirectUris,
    string? ClientType = "confidential",
    string? ApplicationType = "web"
)
{
    public List<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();

        results.AddRange(OAuthAppValidator.ValidateDisplayName(DisplayName, true));
        results.AddRange(OAuthAppValidator.ValidateRedirectUris(RedirectUris, ApplicationType));
        results.AddRange(OAuthAppValidator.ValidateClientType(ClientType));
        results.AddRange(OAuthAppValidator.ValidateApplicationType(ApplicationType));

        return results;
    }
}

/// <summary>
/// OAuth application response
/// </summary>
public record OAuthAppResponse
{
    public string Id { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string ClientType { get; init; } = "confidential";
    public string ApplicationType { get; init; } = "web";
    public string ConsentType { get; init; } = "explicit";
}

/// <summary>
/// OAuth application detail response
/// </summary>
public record OAuthAppDetailResponse
{
    public string Id { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public List<string> RedirectUris { get; init; } = new();
    public string ClientType { get; init; } = "confidential";
    public string ApplicationType { get; init; } = "web";
    public string ConsentType { get; init; } = "explicit";
}

/// <summary>
/// Request to update an OAuth application
/// </summary>
/// <param name="DisplayName">Display name for the application</param>
/// <param name="RedirectUris">List of allowed redirect URIs</param>
/// <param name="ClientType">Client type: "public" or "confidential"</param>
/// <param name="ApplicationType">Application type: "web" or "native"</param>
public record UpdateAppRequest(
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    string DisplayName,
    List<string>? RedirectUris,
    string? ClientType = null,
    string? ApplicationType = null
)
{
    public List<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();

        results.AddRange(OAuthAppValidator.ValidateDisplayName(DisplayName, true));
        results.AddRange(OAuthAppValidator.ValidateRedirectUris(RedirectUris, ApplicationType));
        results.AddRange(OAuthAppValidator.ValidateClientType(ClientType));
        results.AddRange(OAuthAppValidator.ValidateApplicationType(ApplicationType));

        return results;
    }
}

/// <summary>
/// Request to partially update an OAuth application
/// </summary>
/// <param name="DisplayName">Display name for the application</param>
/// <param name="RedirectUris">List of allowed redirect URIs</param>
/// <param name="ClientType">Client type: "public" or "confidential"</param>
/// <param name="ApplicationType">Application type: "web" or "native"</param>
public record PatchAppRequest(
    string? DisplayName = null,
    List<string>? RedirectUris = null,
    string? ClientType = null,
    string? ApplicationType = null
)
{
    public List<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();

        // For PATCH, DisplayName is optional but if provided must be valid
        if (DisplayName != null) results.AddRange(OAuthAppValidator.ValidateDisplayName(DisplayName, false));

        results.AddRange(OAuthAppValidator.ValidateRedirectUris(RedirectUris, ApplicationType));
        results.AddRange(OAuthAppValidator.ValidateClientType(ClientType));
        results.AddRange(OAuthAppValidator.ValidateApplicationType(ApplicationType));

        return results;
    }
}

public record NewSecretResponse(string ClientSecret);

public record CreateAppResponse(
    string Id,
    string ClientId,
    string ClientSecret,
    string DisplayName
);

public record OpenIddictTokenResponse(
    string access_token,
    string token_type,
    int expires_in,
    string? id_token,
    string? refresh_token
);