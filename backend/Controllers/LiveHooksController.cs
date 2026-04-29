using System.Security.Cryptography;
using System.Text;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace backend.Controllers;

public class SrsPublishDto
{
    [JsonPropertyName("stream")] public required string Stream { get; set; }

    [JsonPropertyName("param")] public string? Param { get; set; }
}

public class SrsUnpublishDto
{
    [Required]
    [JsonPropertyName("stream")]
    public string Stream { get; set; } = default!;

    [FromQuery(Name = "secret")] public string? UrlSecret { get; set; }
}

[ApiController]
[Route("live/hooks")]
public class LiveHooksController(BaseServices deps) : BaseApiController(deps)
{
    [HttpPost("publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OnPublish(
        [FromBody] SrsPublishDto body,
        string secret)
    {
        if (!await SettingRegistry.Site.Live.General.Enabled)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Live feature is disabled" });

        if (!int.TryParse(body.Stream, out var roomId) || roomId <= 0)
            return BadRequest(new { message = "Invalid room id in stream field" });

        var (streamKey, _) = ParseSrsParam(body.Param);

        if (!await ValidateHookSecret(secret))
            return Unauthorized(new { message = "Invalid hook secret" });

        if (string.IsNullOrWhiteSpace(streamKey))
            return BadRequest(new { message = "Stream key (token) is missing from push URL" });

        var channel = await _db.LiveChannels.FirstOrDefaultAsync(x => x.OwnerUserId == roomId);
        if (channel == null)
            return NotFound(new { message = "Live channel not found" });

        if (!channel.IsEnabled)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Live channel is disabled" });

        if (string.IsNullOrWhiteSpace(channel.StreamKeyHash))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Channel stream key not configured" });

        var providedHash = ComputeSha256Hex(streamKey.Trim());
        if (!SlowEquals(channel.StreamKeyHash, providedHash))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Invalid stream key" });

        channel.IsLive = true;
        channel.LastLiveAt = DateTime.UtcNow;
        channel.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { code = 0, message = "ok" });
    }

    [HttpPost("unpublish")]
    public async Task<IActionResult> OnUnpublish(
        [FromBody] SrsUnpublishDto body,
        string secret)
    {
        if (!int.TryParse(body.Stream, out var roomId))
            return BadRequest(new { message = "Invalid room id" });

        if (!await ValidateHookSecret(secret))
            return Unauthorized(new { message = "Invalid hook secret" });

        await _db.LiveChannels
            .Where(x => x.OwnerUserId == roomId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsLive, false)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
            );

        return Ok(new { code = 0, message = "ok" });
    }

    private static (string? Key, string? Secret) ParseSrsParam(string? param)
    {
        if (string.IsNullOrWhiteSpace(param)) return (null, null);
        var query = QueryHelpers.ParseQuery(param.Trim());

        query.TryGetValue("key", out var key);
        query.TryGetValue("secret", out var secret);

        return (key.FirstOrDefault(), secret.FirstOrDefault());
    }

    private async Task<bool> ValidateHookSecret(string? provided)
    {
        var expected = await SettingRegistry.Site.Live.Security.HookSecret;
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(provided)) return false;
        return SlowEquals(expected.Trim(), provided.Trim());
    }

    private static string ComputeSha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool SlowEquals(string left, string right)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right)
        );
    }
}
