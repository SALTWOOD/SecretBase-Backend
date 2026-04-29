using System.ComponentModel.DataAnnotations;

namespace backend.Database.Entities;

public class LiveChannel
{
    public int OwnerUserId { get; set; }

    [MaxLength(80)] public string Title { get; set; } = "";

    [MaxLength(1024)] public string? CoverUrl { get; set; }

    public bool IsEnabled { get; set; }

    public bool IsLive { get; set; }

    [MaxLength(128)] public string StreamKeyHash { get; set; } = "";

    public DateTime? LastLiveAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User OwnerUser { get; set; } = null!;
}
