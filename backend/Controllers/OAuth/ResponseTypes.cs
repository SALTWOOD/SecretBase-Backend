namespace backend.Controllers.OAuth;

public record CreateAppRequest(
    string ClientId,
    string DisplayName,
    List<string>? RedirectUris
);

public record OAuthAppResponse
{
    public string Id { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? UserId { get; init; }
}

public record OAuthAppDetailResponse
{
    public string Id { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public List<string> RedirectUris { get; init; } = new();
}

public record UpdateAppRequest(
    string DisplayName,
    List<string>? RedirectUris
);

public record PatchAppRequest(
    string? DisplayName,
    List<string>? RedirectUris
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