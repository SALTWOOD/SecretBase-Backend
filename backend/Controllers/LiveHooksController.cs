using System.Security.Cryptography;
using System.Text;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

public readonly record struct LivePublishHookBody(
    int RoomId,
    string StreamKey
);

public readonly record struct LiveUnpublishHookBody(
    int RoomId
);

[ApiController]
[Route("live/hooks")]
public class LiveHooksController(BaseServices deps) : BaseApiController(deps)
{
    [HttpPost("publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OnPublish([FromBody] LivePublishHookBody body, [FromHeader(Name = "X-Live-Hook-Secret")] string? hookSecret)
    {
        if (!await SettingRegistry.Site.Live.Enabled) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Live feature is disabled" });

        if (!await ValidateHookSecret(hookSecret)) return Unauthorized(new { message = "Invalid hook secret" });

        var channel = await _db.LiveChannels.FirstOrDefaultAsync(x => x.OwnerUserId == body.RoomId);
        if (channel == null) return NotFound(new { message = "Live channel not found" });
        if (!channel.IsEnabled) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Live channel is disabled" });
        if (string.IsNullOrWhiteSpace(channel.StreamKeyHash)) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Stream key is not configured" });

        var providedHash = ComputeSha256Hex(body.StreamKey);
        if (!SlowEquals(channel.StreamKeyHash, providedHash)) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Invalid stream key" });

        channel.IsLive = true;
        channel.LastLiveAt = DateTime.UtcNow;
        channel.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("unpublish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> OnUnpublish([FromBody] LiveUnpublishHookBody body, [FromHeader(Name = "X-Live-Hook-Secret")] string? hookSecret)
    {
        if (!await ValidateHookSecret(hookSecret)) return Unauthorized(new { message = "Invalid hook secret" });

        await _db.LiveChannels
            .Where(x => x.OwnerUserId == body.RoomId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsLive, false)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
            );

        return NoContent();
    }

    private static string ComputeSha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<bool> ValidateHookSecret(string? hookSecret)
    {
        var expected = await SettingRegistry.Site.Live.HookSecret;
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(hookSecret)) return false;

        return SlowEquals(expected.Trim(), hookSecret.Trim());
    }

    private static bool SlowEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
