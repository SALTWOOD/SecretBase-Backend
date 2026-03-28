using System.ComponentModel.DataAnnotations;

namespace backend.Types.Request;

public class ArticleCreateModel
{
    [Required] [MaxLength(200)] public required string Title { get; set; }

    [Required] [MaxLength(10000)] public required string Content { get; set; }

    public bool IsPublished { get; set; } = false;
}