using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using backend.Database.Models;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace backend.Database.Models;

[Table("articles")]
public class Article : BaseModel
{
    [PrimaryKey("id", true)]
    public Guid Id { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("author_id")]
    public Guid AuthorId { get; set; }

    [Column("is_published")]
    public bool IsPublished { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Reference(typeof(Profile), includeInQuery: true, foreignKey: "author_id")]
    public Profile? Author { get; set; }
}