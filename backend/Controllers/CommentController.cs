using backend.Database.Models;
using backend.Services;
using backend.Types.Request;
using backend.Types.Response;
using backend.Types.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Supabase.Postgrest;

namespace backend.Controllers;

[Route("comments")]
public class CommentController(BaseServices deps) : BaseApiController(deps)
{
    [HttpGet("article/{articleId:guid}")]
    [ProducesResponseType(typeof(List<CommentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCommentsByArticle(Guid articleId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var article = await _supa.From<Article>().GetByIdAsync(articleId);
        if (article == null)
        {
            return NotFound(new MessageResponse("Article not found."));
        }

        var comments = await _supa.From<Comment>()
            .Where(it => it.ArticleId == articleId)
            .Page(page, pageSize)
            .Get();

        return Ok(comments.Models);
    }

    [HttpGet("{id:guid}/replies")]
    [ProducesResponseType(typeof(List<CommentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReplies(Guid commentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var parentComment = await _supa.From<Comment>().Where(it => it.Id == commentId).Single();
        if (parentComment == null)
        {
            return NotFound(new MessageResponse("Comment not found."));
        }

        var replies = await _supa.From<Comment>()
            .Where(c => c.ParentCommentId == commentId && !c.IsDeleted)
            .Order("created_at", Constants.Ordering.Descending)
            .Page(page, pageSize)
            .Get();

        return Ok(replies.Models);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateComment([FromBody] CommentCreateModel body, [FromQuery] Guid articleId)
    {
        var userId = await GetCurrentUserIdAsync();

        var article = await _supa.From<Article>().GetByIdAsync(articleId);
        if (article == null)
        {
            return NotFound(new MessageResponse("Article not found."));
        }

        if (body.ParentCommentId.HasValue)
        {
            var parentComment = await _supa.From<Comment>().GetByIdAsync(body.ParentCommentId.Value);
            if (parentComment == null || parentComment.ArticleId != articleId || parentComment.IsDeleted)
            {
                return BadRequest(new MessageResponse("Invalid parent comment."));
            }
        }

        var model = await _supa.From<Comment>().Insert(new Comment
        {
            Content = body.Content,
            ArticleId = articleId,
            AuthorId = userId,
            ParentCommentId = body.ParentCommentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        var comment = model.Model!;

        return CreatedAtAction(nameof(GetCommentsByArticle), new { articleId = comment.ArticleId }, comment );
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateComment(Guid id, [FromBody] CommentUpdateModel body)
    {
        var comment = await _supa.From<Comment>().GetByIdAsync(id);
        if (comment == null)
        {
            return NotFound(new MessageResponse("Comment not found."));
        }

        var userId = await GetCurrentUserIdAsync();
        if (comment.AuthorId != userId)
        {
            return Forbid();
        }

        comment.Content = body.Content;
        comment.UpdatedAt = DateTime.UtcNow;
        
        var model = await _supa.From<Comment>().Update(comment);

        return Ok(model.Model);
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComment(Guid id)
    {
        var comment = await _supa.From<Comment>().GetByIdAsync(id);
        if (comment == null)
        {
            return NotFound(new MessageResponse("Comment not found."));
        }

        var userId = await GetCurrentUserIdAsync();
        if (comment.AuthorId != userId)
        {
            return Forbid();
        }

        comment.IsDeleted = true;
        comment.UpdatedAt = DateTime.UtcNow;

        await _supa.From<Comment>().Update(comment); 

        return Ok(new MessageResponse("Comment deleted successfully."));
    }
}
