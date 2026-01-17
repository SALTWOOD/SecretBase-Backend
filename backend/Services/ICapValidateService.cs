using backend.Models;
using System.Text.Json;

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

    public CapValidateService(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public Task<bool> ValidateAsync(ICaptchaRequest request) => ValidateAsync(request.CaptchaToken);

    public async Task<bool> ValidateAsync(string? token)
    {
        var remoteUrl = _config.GetConnectionString("CapServerConnection");
        var secret = _config.GetConnectionString("CapServerSecret");

        if (string.IsNullOrEmpty(remoteUrl) || string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("Incomplete CapServer Configuration");
        }

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

            var responseString = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<CapRemoteResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Success ?? false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public class CapRemoteResponse
{
    public bool Success { get; set; }
}