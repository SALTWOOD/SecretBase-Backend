using System.ComponentModel.DataAnnotations;

namespace backend.Types.Request;

public class CommentUpdateModel
{
    [Required] [MaxLength(2000)] public required string Content { get; set; }
}