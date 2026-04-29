using System.Collections.Concurrent;
using System.Security.Claims;
using backend.Database;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace backend.Hubs;

public sealed record LiveDanmakuMessage(
    int RoomId,
    string Username,
    string Content,
    string Mode,
    DateTime CreatedAt
);

[Authorize]
public class LiveDanmakuHub(AppDbContext db) : Hub
{
    private static readonly HashSet<string> AllowedModes = ["scroll", "top", "bottom"];
    private static readonly ConcurrentDictionary<int, DateTime> UserLastSentAt = new();

    public async Task JoinRoom(int roomId)
    {
        if (roomId <= 0) throw new HubException("Invalid room id.");

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildRoomGroup(roomId));
    }

    public async Task LeaveRoom(int roomId)
    {
        if (roomId <= 0) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildRoomGroup(roomId));
    }

    public async Task SendDanmaku(int roomId, string content, string mode)
    {
        if (!await SettingRegistry.Site.Live.Enabled)
            throw new HubException("Live feature is disabled.");

        if (!await SettingRegistry.Site.Live.DanmakuEnabled)
            throw new HubException("Danmaku is disabled.");

        if (roomId <= 0) throw new HubException("Invalid room id.");

        var normalizedContent = content?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedContent))
            throw new HubException("Danmaku content cannot be empty.");

        if (normalizedContent.Length > 120)
            throw new HubException("Danmaku is too long.");

        var normalizedMode = mode?.Trim().ToLowerInvariant() ?? "";
        if (!AllowedModes.Contains(normalizedMode))
            throw new HubException("Invalid danmaku mode.");

        var channel = await db.LiveChannels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OwnerUserId == roomId);

        if (channel == null || !channel.IsEnabled)
            throw new HubException("Live room is unavailable.");

        if (!channel.IsLive)
            throw new HubException("Live room is not streaming.");

        var userIdRaw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdRaw, out var userId) || userId <= 0)
            throw new HubException("Unauthorized.");

        var now = DateTime.UtcNow;
        if (UserLastSentAt.TryGetValue(userId, out var lastSentAt))
        {
            var elapsed = now - lastSentAt;
            if (elapsed < TimeSpan.FromMilliseconds(800))
                throw new HubException("Sending too fast.");
        }

        UserLastSentAt[userId] = now;

        var username = Context.User?.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(username))
        {
            username = await db.Users
                .Where(x => x.Id == userId)
                .Select(x => x.Username)
                .FirstOrDefaultAsync();
        }

        if (string.IsNullOrWhiteSpace(username))
            throw new HubException("User not found.");

        var message = new LiveDanmakuMessage(
            roomId,
            username,
            normalizedContent,
            normalizedMode,
            now
        );

        await Clients.Group(BuildRoomGroup(roomId)).SendAsync("danmaku", message);
    }

    private static string BuildRoomGroup(int roomId) => $"live-room:{roomId}";
}
