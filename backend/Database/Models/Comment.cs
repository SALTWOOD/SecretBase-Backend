using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace backend.Database.Models;

[Table("comments")]
public class Comment : BaseModel
{
    [PrimaryKey("id", true)]
    public Guid Id { get; set; }

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("article_id")]
    public Guid ArticleId { get; set; }

    [Column("author_id")]
    public Guid AuthorId { get; set; }

    [Column("parent_comment_id")]
    public Guid? ParentCommentId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Reference(typeof(Profile), foreignKey: "author_id")]
    public Profile? Author { get; set; }

    [Reference(typeof(Comment), foreignKey: "parent_comment_id", includeInQuery: false)]
    public List<Comment> Replies { get; set; } = new();
}