using System.Security.Cryptography;
using System.Text;
using backend.Database.Entities;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

public readonly record struct LiveRoomListItem(
    int RoomId,
    string OwnerUsername,
    string Title,
    string? CoverUrl,
    bool IsLive,
    DateTime? LastLiveAt
);

public readonly record struct LiveRoomDetailsResponse(
    int RoomId,
    int OwnerUserId,
    string OwnerUsername,
    string Title,
    string? CoverUrl,
    bool IsEnabled,
    bool IsLive,
    DateTime? LastLiveAt,
    string PlaybackUrl
);

public readonly record struct MyLiveChannelResponse(
    int RoomId,
    string Title,
    string? CoverUrl,
    bool IsEnabled,
    bool IsLive,
    DateTime? LastLiveAt,
    string RtmpServer,
    string StreamKeyPreview
);

public readonly record struct UpdateMyLiveChannelBody(
    string? Title,
    string? CoverUrl
);

public readonly record struct ResetStreamKeyResponse(
    string StreamKey
);

[Authorize]
[ApiController]
[Route("live")]
public class LiveController(BaseServices deps) : BaseApiController(deps)
{
    [HttpGet("rooms")]
    [ProducesResponseType(typeof(List<LiveRoomListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<LiveRoomListItem>>> GetRooms([FromQuery] bool onlineOnly = true)
    {
        if (!await SettingRegistry.Site.Live.Enabled) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Live feature is disabled" });

        var query = _db.LiveChannels
            .AsNoTracking()
            .Where(x => x.IsEnabled);

        if (onlineOnly) query = query.Where(x => x.IsLive);

        var rooms = await query
            .OrderByDescending(x => x.IsLive)
            .ThenByDescending(x => x.LastLiveAt)
            .Select(x => new LiveRoomListItem(
                x.OwnerUserId,
                x.OwnerUser.Username,
                x.Title,
                x.CoverUrl,
                x.IsLive,
                x.LastLiveAt
            ))
            .ToListAsync();

        return Ok(rooms);
    }

    [HttpGet("rooms/{roomId:int}")]
    [ProducesResponseType(typeof(LiveRoomDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LiveRoomDetailsResponse>> GetRoom(int roomId)
    {
        if (!await SettingRegistry.Site.Live.Enabled) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Live feature is disabled" });

        var channel = await _db.LiveChannels
            .AsNoTracking()
            .Where(x => x.OwnerUserId == roomId && x.IsEnabled)
            .Select(x => new
            {
                x.OwnerUserId,
                x.OwnerUser.Username,
                x.Title,
                x.CoverUrl,
                x.IsEnabled,
                x.IsLive,
                x.LastLiveAt
            })
            .FirstOrDefaultAsync();

        if (channel == null) return NotFound(new { message = "Live room not found" });

        return Ok(new LiveRoomDetailsResponse(
            channel.OwnerUserId,
            channel.OwnerUserId,
            channel.Username,
            channel.Title,
            channel.CoverUrl,
            channel.IsEnabled,
            channel.IsLive,
            channel.LastLiveAt,
            await BuildPlaybackUrl(channel.OwnerUserId)
        ));
    }

    [HttpGet("me/channel")]
    [ProducesResponseType(typeof(MyLiveChannelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MyLiveChannelResponse>> GetMyChannel()
    {
        if (!await SettingRegistry.Site.Live.Enabled) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Live feature is disabled" });

        var user = await CurrentUser;
        if (user == null) return Unauthorized(new { message = "Current user not found" });
        if (!await CanCurrentUserPublish(user)) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only administrators can publish live streams" });

        var channel = await EnsureChannelAsync(user.Id, user.Username);

        return Ok(new MyLiveChannelResponse(
            channel.OwnerUserId,
            channel.Title,
            channel.CoverUrl,
            channel.IsEnabled,
            channel.IsLive,
            channel.LastLiveAt,
            (await SettingRegistry.Site.Live.RtmpServer) ?? "rtmp://localhost/live",
            channel.StreamKeyHash == "" ? "Not generated" : "Generated"
        ));
    }

    [HttpPut("me/channel")]
    [ProducesResponseType(typeof(MyLiveChannelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MyLiveChannelResponse>> UpdateMyChannel([FromBody] UpdateMyLiveChannelBody body)
    {
        if (!await SettingRegistry.Site.Live.Enabled) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Live feature is disabled" });

        var user = await CurrentUser;
        if (user == null) return Unauthorized(new { message = "Current user not found" });
        if (!await CanCurrentUserPublish(user)) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only administrators can publish live streams" });

        var channel = await EnsureChannelAsync(user.Id, user.Username);

        if (!string.IsNullOrWhiteSpace(body.Title)) channel.Title = body.Title.Trim();
        channel.CoverUrl = string.IsNullOrWhiteSpace(body.CoverUrl) ? null : body.CoverUrl.Trim();
        channel.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new MyLiveChannelResponse(
            channel.OwnerUserId,
            channel.Title,
            channel.CoverUrl,
            channel.IsEnabled,
            channel.IsLive,
            channel.LastLiveAt,
            (await SettingRegistry.Site.Live.RtmpServer) ?? "rtmp://localhost/live",
            channel.StreamKeyHash == "" ? "Not generated" : "Generated"
        ));
    }

    [HttpPost("me/channel/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EnableMyChannel()
    {
        if (!await SettingRegistry.Site.Live.Enabled) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Live feature is disabled" });

        var user = await CurrentUser;
        if (user == null) return Unauthorized(new { message = "Current user not found" });
        if (!await CanCurrentUserPublish(user)) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only administrators can publish live streams" });

        var channel = await EnsureChannelAsync(user.Id, user.Username);
        channel.IsEnabled = true;
        channel.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("me/channel/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DisableMyChannel()
    {
        if (!await SettingRegistry.Site.Live.Enabled) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Live feature is disabled" });

        var user = await CurrentUser;
        if (user == null) return Unauthorized(new { message = "Current user not found" });
        if (!await CanCurrentUserPublish(user)) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only administrators can publish live streams" });

        var channel = await EnsureChannelAsync(user.Id, user.Username);
        channel.IsEnabled = false;
        channel.IsLive = false;
        channel.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("me/channel/stream-key/reset")]
    [ProducesResponseType(typeof(ResetStreamKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ResetStreamKeyResponse>> ResetStreamKey()
    {
        if (!await SettingRegistry.Site.Live.Enabled) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Live feature is disabled" });

        var user = await CurrentUser;
        if (user == null) return Unauthorized(new { message = "Current user not found" });
        if (!await CanCurrentUserPublish(user)) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only administrators can publish live streams" });

        var channel = await EnsureChannelAsync(user.Id, user.Username);

        var streamKey = GenerateStreamKey(user.Id);
        channel.StreamKeyHash = ComputeSha256Hex(streamKey);
        channel.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new ResetStreamKeyResponse(streamKey));
    }

    private async Task<bool> CanCurrentUserPublish(User user)
    {
        var adminOnly = await SettingRegistry.Site.Live.AdminOnly;
        if (adminOnly) return user.Role >= UserRole.Admin;

        return user.Role >= UserRole.User;
    }

    private async Task<LiveChannel> EnsureChannelAsync(int userId, string username)
    {
        var channel = await _db.LiveChannels.FirstOrDefaultAsync(x => x.OwnerUserId == userId);
        if (channel != null) return channel;

        channel = new LiveChannel
        {
            OwnerUserId = userId,
            Title = $"{username} 的直播间",
            IsEnabled = false,
            IsLive = false,
            StreamKeyHash = ""
        };

        _db.LiveChannels.Add(channel);
        await _db.SaveChangesAsync();
        return channel;
    }

    private async Task<string> BuildPlaybackUrl(int roomId)
    {
        var baseUrl = await SettingRegistry.Site.Live.PlaybackBaseUrl;
        var normalized = string.IsNullOrWhiteSpace(baseUrl) ? "/hls" : baseUrl.TrimEnd('/');
        return $"{normalized}/{roomId}.m3u8";
    }

    private static string GenerateStreamKey(int userId)
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        var randomPart = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{userId}_{randomPart}";
    }

    private static string ComputeSha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
