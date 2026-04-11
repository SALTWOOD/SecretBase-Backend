using System.ComponentModel.DataAnnotations;

namespace backend.Types.Request;

public class PageUpdateModel
{
    [Required] [MaxLength(200)] public required string Title { get; set; }

    [Required] public required string Content { get; set; }

    [MaxLength(500)] public string? CoverUrl { get; set; }

    public bool IsPublished { get; set; }

    [MaxLength(200)] public string? Slug { get; set; }
}
