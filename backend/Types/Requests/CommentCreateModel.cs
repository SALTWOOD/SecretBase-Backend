using System.ComponentModel.DataAnnotations;

namespace backend.Types.Request;

public class CommentCreateModel : CaptchaRequestBase
{
    [Required] [MaxLength(2000)] public required string Content { get; set; }

    public int? ParentCommentId { get; set; }

    [MaxLength(50)] public string? GuestNickname { get; set; }

    [EmailAddress] public string? GuestEmail { get; set; }

    [Url] public string? GuestWebsite { get; set; }
}
