namespace backend.Types.Response;

public class CommentResponse
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public int ArticleId { get; set; }
    public int? AuthorId { get; set; }
    public string? AuthorUsername { get; set; }
    public int? ParentCommentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public int ReplyCount { get; set; }
    public string? GuestNickname { get; set; }
    public string? GuestWebsite { get; set; }
}
