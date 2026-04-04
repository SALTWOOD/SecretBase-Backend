using backend.Database.Entities;
using backend.Services;
using backend.Types.Request;
using backend.Types.Response;
using backend.Types.Responses;
using ImageProxyClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[Route("articles")]
public class ArticleController(BaseServices deps, IImgproxyClient imgproxyClient) : BaseApiController(deps)
{
    [HttpGet]
    [ProducesResponseType(typeof(List<ArticleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArticles([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.Articles.AsQueryable();

        query = query.Where(a =>
            a.IsPublished || (CurrentUserId.HasValue && a.AuthorId == CurrentUserId));

        var totalCount = await query.CountAsync();

        var articles = await query
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
                CommentCount = a.Comments.Count
            })
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", totalCount.ToString());

        return Ok(articles);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ArticleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArticle(int id)
    {
        var article = await _db.Articles
            .Where(a => a.Id == id)
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
                CommentCount = a.Comments.Count
            })
            .FirstOrDefaultAsync();

        if (article == null) return NotFound(new MessageResponse("Article not found."));

        return Ok(article);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ArticleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateArticle([FromBody] ArticleCreateModel model)
    {
        if (CurrentUserId == null) return BadRequest(new MessageResponse("User not authenticated."));

        var article = new Article
        {
            Title = model.Title,
            Content = model.Content,
            AuthorId = CurrentUserId.Value,
            IsPublished = model.IsPublished,
            CoverUrl = model.CoverUrl,
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
            CommentCount = 0
        };

        return CreatedAtAction(nameof(GetArticle), new { id = article.Id }, response);
    }

    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ArticleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateArticle(int id, [FromBody] ArticleUpdateModel model)
    {
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return NotFound(new MessageResponse("Article not found."));

        if (CurrentUserId == null) return BadRequest(new MessageResponse("User not authenticated."));

        if (article.AuthorId != CurrentUserId.Value) return Forbid();

        article.Title = model.Title;
        article.Content = model.Content;
        article.IsPublished = model.IsPublished;
        article.CoverUrl = model.CoverUrl;
        article.UpdatedAt = DateTime.UtcNow;

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
            CommentCount = article.Comments.Count
        };

        return Ok(response);
    }

    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteArticle(int id)
    {
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return NotFound(new MessageResponse("Article not found."));

        if (CurrentUserId == null) return BadRequest(new MessageResponse("User not authenticated."));

        if (article.AuthorId != CurrentUserId.Value) return Forbid();

        _db.Articles.Remove(article);
        await _db.SaveChangesAsync();

        return Ok(new MessageResponse("Article deleted successfully."));
    }

    [HttpGet("{id}/cover")]
    public async Task<IActionResult> GetCover(int id)
    {
        var article = await _db.Articles.FindAsync(id);
        var url = article?.CoverUrl;
        if (url == null) return NotFound(new MessageResponse("Article not found or missing cover image."));
        Uri uri = new Uri(url);
        switch (uri.Scheme.ToLower())
        {
            case "s3":
                var image = imgproxyClient.BuildUrl(url, options =>
                    options.Resize(1200, 675, ResizeMode.Fit)
                        .Format(ImageFormat.WebP)
                        .Expires(DateTime.UtcNow.AddHours(1))
                );
                return Redirect(image);
            default:
                return Redirect(url);
        }
    }
}