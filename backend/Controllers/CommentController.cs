using backend.Database.Entities;
using backend.Filters;
using backend.Services;
using backend.Types.Request;
using backend.Types.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[Route("comments")]
public class CommentController(BaseServices deps) : BaseApiController(deps)
{
    [HttpGet("article/{articleId}")]
    [ProducesResponseType(typeof(List<CommentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCommentsByArticle(int articleId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var article = await _db.Articles.FindAsync(articleId);
        if (article == null) return NotFound(new MessageResponse("Article not found."));

        var comments = await _db.Comments
            .Where(c => c.ArticleId == articleId && !c.IsDeleted && c.ReviewStatus == ReviewStatus.Approved &&
                        c.ParentCommentId == null)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CommentResponse
            {
                Id = c.Id,
                Content = c.Content,
                ArticleId = c.ArticleId,
                AuthorId = c.AuthorId,
                AuthorUsername = c.Author != null ? c.Author.Username : null,
                ParentCommentId = c.ParentCommentId,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                IsDeleted = c.IsDeleted,
                ReplyCount = c.Replies.Count(r => !r.IsDeleted && r.ReviewStatus == ReviewStatus.Approved),
                GuestNickname = c.Metadata.GuestNickname,
                GuestWebsite = c.Metadata.GuestWebsite
            })
            .ToListAsync();

        return Ok(comments);
    }

    [HttpGet("{commentId}/replies")]
    [ProducesResponseType(typeof(List<CommentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReplies(int commentId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var parentComment = await _db.Comments.FindAsync(commentId);
        if (parentComment == null) return NotFound(new MessageResponse("Comment not found."));

        var replies = await _db.Comments
            .Where(c => c.ParentCommentId == commentId && !c.IsDeleted && c.ReviewStatus == ReviewStatus.Approved)
            .OrderBy(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CommentResponse
            {
                Id = c.Id,
                Content = c.Content,
                ArticleId = c.ArticleId,
                AuthorId = c.AuthorId,
                AuthorUsername = c.Author != null ? c.Author.Username : null,
                ParentCommentId = c.ParentCommentId,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                IsDeleted = c.IsDeleted,
                ReplyCount = c.Replies.Count(r => !r.IsDeleted && r.ReviewStatus == ReviewStatus.Approved),
                GuestNickname = c.Metadata.GuestNickname,
                GuestWebsite = c.Metadata.GuestWebsite
            })
            .ToListAsync();

        return Ok(replies);
    }

    [HttpPost]
    [ValidateCaptcha]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateComment([FromBody] CommentCreateModel model,
        [FromQuery] int articleId)
    {
        var article = await _db.Articles.FindAsync(articleId);
        if (article == null) return NotFound(new MessageResponse("Article not found."));

        if (model.ParentCommentId.HasValue)
        {
            var parentComment = await _db.Comments.FindAsync(model.ParentCommentId.Value);
            if (parentComment == null || parentComment.ArticleId != articleId || parentComment.IsDeleted)
                return BadRequest(new MessageResponse("Invalid parent comment."));
        }

        var isGuest = CurrentUserId == null;

        if (isGuest)
        {
            var guestEnabled = await SettingRegistry.Site.Comment.Guest.Enabled;
            if (!guestEnabled)
                return BadRequest(new MessageResponse("Guest comments are not enabled."));

            if (string.IsNullOrWhiteSpace(model.GuestNickname))
                return BadRequest(new MessageResponse("Guest nickname is required."));

            if (model.ParentCommentId.HasValue)
            {
                var allowReply = await SettingRegistry.Site.Comment.Guest.AllowReply;
                if (!allowReply)
                    return BadRequest(new MessageResponse("Guest replies are not allowed."));
            }
        }

        var requireApproval = isGuest && await SettingRegistry.Site.Comment.Guest.RequireApproval;

        var metadata = new CommentMetadata
        {
            GuestNickname = isGuest ? model.GuestNickname : null,
            GuestEmail = isGuest ? model.GuestEmail : null,
            GuestWebsite = isGuest ? model.GuestWebsite : null,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var comment = new Comment
        {
            Content = model.Content,
            ArticleId = articleId,
            AuthorId = CurrentUserId,
            ParentCommentId = model.ParentCommentId,
            ReviewStatus = requireApproval ? ReviewStatus.Pending : ReviewStatus.Approved,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        var authorUsername = CurrentUserId.HasValue
            ? (await _db.Users.FindAsync(CurrentUserId.Value))?.Username
            : null;

        var response = new CommentResponse
        {
            Id = comment.Id,
            Content = comment.Content,
            ArticleId = comment.ArticleId,
            AuthorId = comment.AuthorId,
            AuthorUsername = authorUsername,
            ParentCommentId = comment.ParentCommentId,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            IsDeleted = comment.IsDeleted,
            ReplyCount = 0,
            GuestNickname = comment.Metadata.GuestNickname,
            GuestWebsite = comment.Metadata.GuestWebsite
        };

        return CreatedAtAction(nameof(GetCommentsByArticle), new { articleId = comment.ArticleId }, response);
    }

    [HttpPut("{id}")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateComment(int id, [FromBody] CommentUpdateModel model)
    {
        var comment = await _db.Comments.FindAsync(id);
        if (comment == null) return NotFound(new MessageResponse("Comment not found."));

        if (CurrentUserId == null) return BadRequest(new MessageResponse("User not authenticated."));

        if (comment.AuthorId != CurrentUserId.Value) return Forbid();

        comment.Content = model.Content;
        comment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var response = new CommentResponse
        {
            Id = comment.Id,
            Content = comment.Content,
            ArticleId = comment.ArticleId,
            AuthorId = comment.AuthorId,
            AuthorUsername = (await _db.Users.FindAsync(comment.AuthorId))?.Username,
            ParentCommentId = comment.ParentCommentId,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            IsDeleted = comment.IsDeleted,
            ReplyCount = comment.Replies.Count(r => !r.IsDeleted && r.ReviewStatus == ReviewStatus.Approved),
            GuestNickname = comment.Metadata.GuestNickname,
            GuestWebsite = comment.Metadata.GuestWebsite
        };

        return Ok(response);
    }

    [HttpDelete("{id}")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var comment = await _db.Comments.FindAsync(id);
        if (comment == null) return NotFound(new MessageResponse("Comment not found."));

        if (CurrentUserId == null) return BadRequest(new MessageResponse("User not authenticated."));

        if (comment.AuthorId != CurrentUserId.Value) return Forbid();

        comment.IsDeleted = true;
        comment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new MessageResponse("Comment deleted successfully."));
    }
}
