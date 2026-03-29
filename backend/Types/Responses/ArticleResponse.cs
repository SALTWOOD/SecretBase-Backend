namespace backend.Types.Responses;

public class ArticleResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int AuthorId { get; set; }
    public string? AuthorUsername { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsPublished { get; set; }
    public string? CoverUrl { get; set; }
    public string Cover => $"/api/v1/articles/{Id}/cover";
    public int CommentCount { get; set; }
}