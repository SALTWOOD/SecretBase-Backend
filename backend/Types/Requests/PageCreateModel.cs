using System.ComponentModel.DataAnnotations;

namespace backend.Types.Request;

public class PageCreateModel
{
    [Required] [MaxLength(200)] public required string Title { get; set; }

    [Required] public required string Content { get; set; }

    [MaxLength(500)] public string? CoverUrl { get; set; }

    public bool IsPublished { get; set; } = false;

    [Required] [MaxLength(200)] public required string Slug { get; set; }
}
