using System.ComponentModel.DataAnnotations;

namespace backend.Database.Entities;

public class FileShare
{
    [MaxLength(32)] public string ShortId { get; set; } = string.Empty;

    [MaxLength(255)] public string Bucket { get; set; } = string.Empty;

    [MaxLength(1024)] public string Key { get; set; } = string.Empty;

    [MaxLength(255)] public string FileName { get; set; } = string.Empty;

    [MaxLength(128)] public string? Remarks { get; set; }

    public bool IsPublic { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public int OwnerId { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Owner { get; set; } = null!;
}