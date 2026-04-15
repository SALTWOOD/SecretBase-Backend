using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using backend.Database;
using backend.Database.Entities;
using backend.Services;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static backend.Services.SessionService;

namespace backend.Controllers.Auth;

[Route("auth/github")]
public class GitHubAuthController(BaseServices deps) : BaseApiController(deps)
{
    private const string Provider = "GitHub";
    private const string StatePrefix = "github_oauth:state:";
    private const int StateTtlMinutes = 5;

    private static readonly HashSet<string> RequiredSettings = ["site.user.github.client_id", "site.user.github.client_secret"];

    [HttpGet("login")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> Login()
    {
        var (clientId, _, error) = await GetGitHubSettings();
        if (error != null) return error;

        var state = Utils.GenerateRandomSecret(32);
        await StoreStateAsync(state, new OAuthState { Action = "login" });

        return Redirect(BuildGitHubAuthorizeUrl(clientId!, state));
    }

    [HttpGet("bind")]
    [Authorize(Policy = "CookieOnly")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> Bind()
    {
        var (clientId, _, error) = await GetGitHubSettings();
        if (error != null) return error;

        var state = Utils.GenerateRandomSecret(32);
        var userId = CurrentUserId ?? throw new InvalidOperationException("User ID is required for binding.");
        await StoreStateAsync(state, new OAuthState { Action = "bind", UserId = userId });

        return Redirect(BuildGitHubAuthorizeUrl(clientId!, state));
    }

    [HttpGet("callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> Callback(string? code, string? state)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect("/auth/login?error=github_callback_failed");

        var oauthState = await GetAndDeleteStateAsync(state);
        if (oauthState == null)
            return Redirect("/auth/login?error=github_state_expired");

        var (clientId, clientSecret, error) = await GetGitHubSettings();
        if (error != null) return Redirect("/auth/login?error=github_misconfigured");

        var token = await ExchangeCodeForTokenAsync(clientId!, clientSecret!, code);
        if (token == null)
            return Redirect("/auth/login?error=github_token_failed");

        var githubUser = await GetGitHubUserAsync(token);
        if (githubUser == null)
            return Redirect("/auth/login?error=github_user_failed");

        var githubId = githubUser.Value.GetProperty("id").ToString()!;
        var githubLogin = githubUser.Value.GetProperty("login").GetString()!;
        var githubAvatar = githubUser.Value.TryGetProperty("avatar_url", out var avatarProp)
            ? avatarProp.GetString()
            : null;

        switch (oauthState.Action)
        {
            case "bind":
                if (oauthState.UserId == null)
                    return Redirect("/auth/login?error=github_invalid_action");
                return await HandleBind(oauthState.UserId.Value, githubId, githubLogin, githubAvatar, token);
            case "login":
                return await HandleLogin(githubId, githubLogin, githubAvatar, token);
            default:
                return Redirect("/auth/login?error=github_invalid_action");
        }
    }

    private async Task<IActionResult> HandleBind(int userId, string githubId, string githubLogin,
        string? githubAvatar, string accessToken)
    {
        var existingBinding = await _db.ThirdPartyBindings
            .FirstOrDefaultAsync(b => b.Provider == Provider && b.ProviderUserId == githubId);

        if (existingBinding != null)
        {
            if (existingBinding.UserId == userId)
                return Redirect("/dash/user/profile?github_status=already_bound");

            return Redirect("/dash/user/profile?github_status=bound_by_other");
        }

        var binding = new ThirdPartyBinding
        {
            UserId = userId,
            Provider = Provider,
            ProviderUserId = githubId,
            ProviderUsername = githubLogin,
            ProviderAvatarUrl = githubAvatar,
            AccessToken = accessToken
        };

        _db.ThirdPartyBindings.Add(binding);
        await _db.SaveChangesAsync();

        return Redirect("/dash/user/profile?github_status=bound");
    }

    private async Task<IActionResult> HandleLogin(string githubId, string githubLogin,
        string? githubAvatar, string accessToken)
    {
        var binding = await _db.ThirdPartyBindings
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Provider == Provider && b.ProviderUserId == githubId);

        if (binding == null)
            return Redirect("/auth/login?error=github_not_bound");

        var user = binding.User;
        if (user.IsBanned)
            return Redirect("/auth/login?error=user_banned");

        binding.ProviderUsername = githubLogin;
        binding.ProviderAvatarUrl = githubAvatar;
        binding.AccessToken = accessToken;
        await _db.SaveChangesAsync();

        await UpdateLastLoginAsync(user, HttpContext);
        await RefreshTokenAsync(user);

        return Redirect("/dash");
    }

    private async Task<(string? ClientId, string? ClientSecret, IActionResult? Error)> GetGitHubSettings()
    {
        var clientId = await SettingRegistry.Site.User.Github.ClientId;
        var clientSecret = await SettingRegistry.Site.User.Github.ClientSecret;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return (null, null, BadRequest(new MessageResponse("GitHub OAuth is not configured.")));

        return (clientId, clientSecret, null);
    }

    private async Task StoreStateAsync(string state, OAuthState oauthState)
    {
        var key = $"{StatePrefix}{state}";
        var json = JsonSerializer.Serialize(oauthState);
        await _redis.StringSetAsync(key, json, TimeSpan.FromMinutes(StateTtlMinutes));
    }

    private async Task<OAuthState?> GetAndDeleteStateAsync(string state)
    {
        var key = $"{StatePrefix}{state}";
        var data = await _redis.StringGetAsync(key);
        if (data.IsNullOrEmpty) return null;

        await _redis.KeyDeleteAsync(key);
        return JsonSerializer.Deserialize<OAuthState>(data.ToString());
    }

    private static string BuildGitHubAuthorizeUrl(string clientId, string state)
    {
        return $"https://github.com/login/oauth/authorize?client_id={clientId}&state={state}&scope=read:user%2Cuser:email";
    }

    private async Task<string?> ExchangeCodeForTokenAsync(string clientId, string clientSecret, string code)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var body = new
        {
            client_id = clientId,
            client_secret = clientSecret,
            code
        };

        var response = await httpClient.PostAsJsonAsync("https://github.com/login/oauth/access_token", body);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("access_token", out var tokenProp) ? tokenProp.GetString() : null;
    }

    private async Task<JsonElement?> GetGitHubUserAsync(string accessToken)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SecretBase");

        var response = await httpClient.GetAsync("https://api.github.com/user");
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}

public class OAuthState
{
    public string Action { get; set; } = string.Empty;
    public int? UserId { get; set; }
}
