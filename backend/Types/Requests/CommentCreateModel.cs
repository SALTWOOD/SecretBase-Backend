using System.ComponentModel.DataAnnotations;

namespace backend.Types.Request;

public class CommentCreateModel
{
    [Required] [MaxLength(2000)] public required string Content { get; set; }

    public int? ParentCommentId { get; set; }
}