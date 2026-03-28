using backend.Database.Entities;
using backend.Services;
using backend.Types.Request;
using backend.Types.Response;
using backend.Types.Responses;
using Microsoft.AspNetCore.Authorization;
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
            .Where(c => c.ArticleId == articleId && !c.IsDeleted && c.ParentCommentId == null)
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
                ReplyCount = c.Replies.Count(r => !r.IsDeleted)
            })
            .ToListAsync();

        return Ok(comments);
    }

    [HttpGet("{id}/replies")]
    [ProducesResponseType(typeof(List<CommentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReplies(int commentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var parentComment = await _db.Comments.FindAsync(commentId);
        if (parentComment == null) return NotFound(new MessageResponse("Comment not found."));

        var replies = await _db.Comments
            .Where(c => c.ParentCommentId == commentId && !c.IsDeleted)
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
                ReplyCount = c.Replies.Count(r => !r.IsDeleted)
            })
            .ToListAsync();

        return Ok(replies);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateComment([FromBody] CommentCreateModel model, [FromQuery] int articleId)
    {
        if (CurrentUserId == null) return BadRequest(new MessageResponse("User not authenticated."));

        var article = await _db.Articles.FindAsync(articleId);
        if (article == null) return NotFound(new MessageResponse("Article not found."));

        if (model.ParentCommentId.HasValue)
        {
            var parentComment = await _db.Comments.FindAsync(model.ParentCommentId.Value);
            if (parentComment == null || parentComment.ArticleId != articleId || parentComment.IsDeleted)
                return BadRequest(new MessageResponse("Invalid parent comment."));
        }

        var comment = new Comment
        {
            Content = model.Content,
            ArticleId = articleId,
            AuthorId = CurrentUserId.Value,
            ParentCommentId = model.ParentCommentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
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
            ReplyCount = 0
        };

        return CreatedAtAction(nameof(GetCommentsByArticle), new { articleId = comment.ArticleId }, response);
    }

    [HttpPut("{id}")]
    [Authorize]
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
            ReplyCount = comment.Replies.Count(r => !r.IsDeleted)
        };

        return Ok(response);
    }

    [HttpDelete("{id}")]
    [Authorize]
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