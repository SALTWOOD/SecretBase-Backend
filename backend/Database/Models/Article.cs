using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace backend.Database.Entities;

public class Article
{
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(10000)]
    public required string Content { get; set; }

    public int AuthorId { get; set; }

    [JsonIgnore]
    public User? Author { get; set; }

    public List<Comment> Comments { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsPublished { get; set; } = false;
}
