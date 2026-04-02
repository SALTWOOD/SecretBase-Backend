using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace backend.Database.Entities;

public enum ReviewStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public class CommentMetadata
{
    public string? GuestNickname { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestWebsite { get; set; }
    public string? IpAddress { get; set; }
}

public class Comment
{
    public int Id { get; set; }

    [MaxLength(2000)] public required string Content { get; set; }

    public int ArticleId { get; set; }

    [JsonIgnore] public Article? Article { get; set; }

    public int? AuthorId { get; set; }

    [JsonIgnore] public User? Author { get; set; }

    public int? ParentCommentId { get; set; }

    [JsonIgnore] public Comment? ParentComment { get; set; }

    public List<Comment> Replies { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;

    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;

    public CommentMetadata Metadata { get; set; } = new();
}