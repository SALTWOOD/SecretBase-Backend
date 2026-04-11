using backend.Database.Entities;
using backend.Services;
using backend.Types.Request;
using backend.Types.Response;
using backend.Types.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[Route("pages")]
public class PageController(BaseServices deps) : BaseApiController(deps)
{
    [HttpGet]
    [ProducesResponseType(typeof(List<ArticleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPages([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var query = _db.Articles.AsQueryable();

        query = query.Where(a => a.Type == ArticleType.Page);

        query = query.Where(a =>
            a.IsPublished || (CurrentUserId.HasValue && a.AuthorId == CurrentUserId));

        var totalCount = await query.CountAsync();

        var pages = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ArticleResponse
            {
                Id = a.Id,
                Title = a.Title,
                AuthorId = a.AuthorId,
                AuthorUsername = a.Author != null ? a.Author.Username : null,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                IsPublished = a.IsPublished,
                CoverUrl = a.CoverUrl,
                CommentCount = a.Comments.Count,
                Type = a.Type,
                Slug = a.Slug
            })
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", totalCount.ToString());

        return Ok(pages);
    }

    [HttpGet("slug/{slug}")]
    [ProducesResponseType(typeof(ArticleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPageBySlug(string slug)
    {
        var page = await _db.Articles
            .Where(a => a.Slug == slug && a.Type == ArticleType.Page && a.IsPublished)
            .Select(a => new ArticleResponse
            {
                Id = a.Id,
                Title = a.Title,
                Content = a.Content,
                AuthorId = a.AuthorId,
                AuthorUsername = a.Author != null ? a.Author.Username : null,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                IsPublished = a.IsPublished,
                CoverUrl = a.CoverUrl,
                CommentCount = a.Comments.Count,
                Type = a.Type,
                Slug = a.Slug
            })
            .FirstOrDefaultAsync();

        if (page == null) return NotFound(new MessageResponse("Page not found."));

        return Ok(page);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ArticleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPage(int id)
    {
        var page = await _db.Articles
            .Where(a => a.Id == id && a.Type == ArticleType.Page)
            .Select(a => new ArticleResponse
            {
                Id = a.Id,
                Title = a.Title,
                Content = a.Content,
                AuthorId = a.AuthorId,
                AuthorUsername = a.Author != null ? a.Author.Username : null,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                IsPublished = a.IsPublished,
                CoverUrl = a.CoverUrl,
                CommentCount = a.Comments.Count,
                Type = a.Type,
                Slug = a.Slug
            })
            .FirstOrDefaultAsync();

        if (page == null) return NotFound(new MessageResponse("Page not found."));

        return Ok(page);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ArticleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePage([FromBody] PageCreateModel model)
    {
        if (CurrentUserId == null) return BadRequest(new MessageResponse("User not authenticated."));

        if (string.IsNullOrWhiteSpace(model.Slug))
            return BadRequest(new MessageResponse("Slug is required for pages."));

        if (await _db.Articles.AnyAsync(a => a.Slug == model.Slug))
            return BadRequest(new MessageResponse("Slug already exists."));

        var article = new Article
        {
            Title = model.Title,
            Content = model.Content,
            AuthorId = CurrentUserId.Value,
            IsPublished = model.IsPublished,
            CoverUrl = model.CoverUrl,
            Slug = model.Slug,
            Type = ArticleType.Page,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Articles.Add(article);
        await _db.SaveChangesAsync();

        var response = new ArticleResponse
        {
            Id = article.Id,
            Title = article.Title,
            Content = article.Content,
            AuthorId = article.AuthorId,
            AuthorUsername = (await _db.Users.FindAsync(article.AuthorId))?.Username,
            CreatedAt = article.CreatedAt,
            UpdatedAt = article.UpdatedAt,
            IsPublished = article.IsPublished,
            CoverUrl = article.CoverUrl,
            CommentCount = 0,
            Type = article.Type,
            Slug = article.Slug
        };

        return CreatedAtAction(nameof(GetPage), new { id = article.Id }, response);
    }

    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ArticleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePage(int id, [FromBody] PageUpdateModel model)
    {
        var page = await _db.Articles.FindAsync(id);
        if (page == null) return NotFound(new MessageResponse("Page not found."));

        if (page.Type != ArticleType.Page) return NotFound(new MessageResponse("Page not found."));

        if (CurrentUserId == null) return BadRequest(new MessageResponse("User not authenticated."));

        if (page.AuthorId != CurrentUserId.Value) return Forbid();

        if (!string.IsNullOrWhiteSpace(model.Slug) &&
            await _db.Articles.AnyAsync(a => a.Slug == model.Slug && a.Id != id))
            return BadRequest(new MessageResponse("Slug already exists."));

        page.Title = model.Title;
        page.Content = model.Content;
        page.IsPublished = model.IsPublished;
        page.CoverUrl = model.CoverUrl;
        page.Slug = model.Slug;
        page.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var response = new ArticleResponse
        {
            Id = page.Id,
            Title = page.Title,
            Content = page.Content,
            AuthorId = page.AuthorId,
            AuthorUsername = (await _db.Users.FindAsync(page.AuthorId))?.Username,
            CreatedAt = page.CreatedAt,
            UpdatedAt = page.UpdatedAt,
            IsPublished = page.IsPublished,
            CoverUrl = page.CoverUrl,
            CommentCount = page.Comments.Count,
            Type = page.Type,
            Slug = page.Slug
        };

        return Ok(response);
    }

    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePage(int id)
    {
        var page = await _db.Articles.FindAsync(id);
        if (page == null) return NotFound(new MessageResponse("Page not found."));

        if (page.Type != ArticleType.Page) return NotFound(new MessageResponse("Page not found."));

        if (CurrentUserId == null) return BadRequest(new MessageResponse("User not authenticated."));

        if (page.AuthorId != CurrentUserId.Value) return Forbid();

        _db.Articles.Remove(page);
        await _db.SaveChangesAsync();

        return Ok(new MessageResponse("Page deleted successfully."));
    }
}
