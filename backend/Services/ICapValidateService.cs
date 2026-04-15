using backend.Types.Request;
using System.Text.Json.Serialization;

namespace backend.Services;

public interface ICapValidateService
{
    Task<bool> ValidateAsync(string? token);
    Task<bool> ValidateAsync(ICaptchaRequest request);
}

public class CapValidateService : ICapValidateService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CapValidateService> _logger;

    public CapValidateService(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<CapValidateService> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<bool> ValidateAsync(ICaptchaRequest request)
    {
        return ValidateAsync(request.CaptchaToken);
    }

    public async Task<bool> ValidateAsync(string? token)
    {
        var remoteUrl = _config.GetConnectionString("CapServerConnection");
        var secret = _config.GetConnectionString("CapServerSecret");

        if (string.IsNullOrEmpty(remoteUrl) || string.IsNullOrEmpty(secret))
            throw new InvalidOperationException("Incomplete CapServer Configuration");

        var requestData = new
        {
            token = token,
            secret
        };

        var client = _httpClientFactory.CreateClient();

        try
        {
            var response = await client.PostAsJsonAsync(remoteUrl, requestData);

            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<CapRemoteResponse>();

            return result?.Success ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Captcha validation failed");
            return false;
        }
    }
}

public class CapRemoteResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
}