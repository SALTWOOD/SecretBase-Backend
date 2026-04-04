using backend.Database.Entities;
using backend.Services;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers.Admin;

public class AdminCommentResponse
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public int ArticleId { get; set; }
    public int? AuthorId { get; set; }
    public string? AuthorUsername { get; set; }
    public int? ParentCommentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public ReviewStatus ReviewStatus { get; set; }
    public string? GuestNickname { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestWebsite { get; set; }
    public string? IpAddress { get; set; }
}

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("admin/comments")]
public class AdminCommentsController(BaseServices deps) : BaseApiController(deps)
{
    [HttpGet]
    [ProducesResponseType(typeof(List<AdminCommentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllComments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] ReviewStatus? status = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Comments.AsQueryable();

        if (status.HasValue)
            query = query.Where(c => c.ReviewStatus == status.Value);

        var totalCount = await query.CountAsync();

        var comments = await query
            .Include(c => c.Author)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new AdminCommentResponse
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
                ReviewStatus = c.ReviewStatus,
                GuestNickname = c.Metadata.GuestNickname,
                GuestEmail = c.Metadata.GuestEmail,
                GuestWebsite = c.Metadata.GuestWebsite,
                IpAddress = c.Metadata.IpAddress
            })
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", totalCount.ToString());

        return Ok(comments);
    }

    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<AdminCommentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingComments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Comments.Where(c => c.ReviewStatus == ReviewStatus.Pending);

        var totalCount = await query.CountAsync();

        var comments = await query
            .Include(c => c.Author)
            .OrderBy(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new AdminCommentResponse
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
                ReviewStatus = c.ReviewStatus,
                GuestNickname = c.Metadata.GuestNickname,
                GuestEmail = c.Metadata.GuestEmail,
                GuestWebsite = c.Metadata.GuestWebsite,
                IpAddress = c.Metadata.IpAddress
            })
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", totalCount.ToString());

        return Ok(comments);
    }

    [HttpPut("{id}/approve")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveComment(int id)
    {
        var comment = await _db.Comments.FindAsync(id);
        if (comment == null) return NotFound(new MessageResponse("Comment not found."));

        comment.ReviewStatus = ReviewStatus.Approved;
        comment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new MessageResponse("Comment approved."));
    }

    [HttpPut("{id}/reject")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectComment(int id)
    {
        var comment = await _db.Comments.FindAsync(id);
        if (comment == null) return NotFound(new MessageResponse("Comment not found."));

        comment.ReviewStatus = ReviewStatus.Rejected;
        comment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new MessageResponse("Comment rejected."));
    }
}
