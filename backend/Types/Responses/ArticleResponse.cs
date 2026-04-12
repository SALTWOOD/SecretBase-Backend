using backend.Database.Entities;
using backend.Types.Responses;

namespace backend.Types.Responses;

public class ArticleResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public UserDto Author { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsPublished { get; set; }
    public string? CoverUrl { get; set; }
    public int CommentCount { get; set; }
    public ArticleType Type { get; set; }
    public string? Slug { get; set; }
}