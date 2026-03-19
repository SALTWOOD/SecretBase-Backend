using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace backend.Database.Entities;

public class Comment
{
    public int Id { get; set; }

    [MaxLength(2000)]
    public required string Content { get; set; }

    public int ArticleId { get; set; }

    [JsonIgnore]
    public Article? Article { get; set; }

    public int AuthorId { get; set; }

    [JsonIgnore]
    public User? Author { get; set; }

    public int? ParentCommentId { get; set; }

    [JsonIgnore]
    public Comment? ParentComment { get; set; }

    public List<Comment> Replies { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;
}
