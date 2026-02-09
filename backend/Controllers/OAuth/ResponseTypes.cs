namespace backend.Controllers.OAuth;

public record CreateAppRequest(
    string ClientId,
    string ClientSecret,
    string DisplayName,
    List<string>? RedirectUris
);

public record OAuthAppResponse
{
    public string Id { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public record OpenIddictTokenResponse(
    string access_token,
    string token_type,
    int expires_in,
    string? id_token,
    string? refresh_token
);