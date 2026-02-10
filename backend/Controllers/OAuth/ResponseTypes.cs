namespace backend.Controllers.OAuth;

/// <summary>
/// Request to create a new OAuth application
/// </summary>
/// <param name="DisplayName">Display name for the application</param>
/// <param name="RedirectUris">List of allowed redirect URIs</param>
/// <param name="ClientType">Client type: "public" or "confidential" (default: "confidential")</param>
/// <param name="ApplicationType">Application type: "web" or "native" (default: "web")</param>
/// <param name="ConsentType">Consent type: "implicit", "explicit", "external", or "systematic" (default: "explicit")</param>
public record CreateAppRequest(
    string DisplayName,
    List<string>? RedirectUris,
    string? ClientType = "confidential",
    string? ApplicationType = "web",
    string? ConsentType = "explicit"
);

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
/// <param name="ConsentType">Consent type: "implicit", "explicit", "external", or "systematic"</param>
public record UpdateAppRequest(
    string DisplayName,
    List<string>? RedirectUris,
    string? ClientType = null,
    string? ApplicationType = null,
    string? ConsentType = null
);

/// <summary>
/// Request to partially update an OAuth application
/// </summary>
/// <param name="DisplayName">Display name for the application</param>
/// <param name="RedirectUris">List of allowed redirect URIs</param>
/// <param name="ClientType">Client type: "public" or "confidential"</param>
/// <param name="ApplicationType">Application type: "web" or "native"</param>
/// <param name="ConsentType">Consent type: "implicit", "explicit", "external", or "systematic"</param>
public record PatchAppRequest(
    string? DisplayName = null,
    List<string>? RedirectUris = null,
    string? ClientType = null,
    string? ApplicationType = null,
    string? ConsentType = null
);

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