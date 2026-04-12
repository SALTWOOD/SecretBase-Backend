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

        query = query.Where(a => a.Type == ArticleType.Post);

        query = query.Where(a =>
            a.IsPublished || (CurrentUserId.HasValue && a.AuthorId == CurrentUserId));

        var totalCount = await query.CountAsync();

        var articlesRaw = await query
            .Include(a => a.Author)
            .Include(a => a.Comments)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var articles = articlesRaw.Select(ArticleMappingHelper.ToListResponse).ToList();

        Response.Headers.Append("X-Total-Count", totalCount.ToString());

        return Ok(articles);
    }

    [HttpGet("slug/{slug}")]
    [ProducesResponseType(typeof(ArticleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArticleBySlug(string slug)
    {
        var article = await _db.Articles
            .Include(a => a.Author)
            .Include(a => a.Comments)
            .Where(a => a.Slug == slug && a.Type == ArticleType.Post)
            .FirstOrDefaultAsync();

        if (article == null) return NotFound(new MessageResponse("Article not found."));

        return Ok(ArticleMappingHelper.ToDetailResponse(article));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ArticleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArticle(int id)
    {
        var article = await _db.Articles
            .Include(a => a.Author)
            .Include(a => a.Comments)
            .Where(a => a.Id == id && a.Type == ArticleType.Post)
            .FirstOrDefaultAsync();

        if (article == null) return NotFound(new MessageResponse("Article not found."));

        return Ok(ArticleMappingHelper.ToDetailResponse(article));
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ArticleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateArticle([FromBody] ArticleCreateModel model)
    {
        if (CurrentUserId == null) return BadRequest(new MessageResponse("User not authenticated."));

        if (!string.IsNullOrWhiteSpace(model.Slug) &&
            await _db.Articles.AnyAsync(a => a.Slug == model.Slug))
            return BadRequest(new MessageResponse("Slug already exists."));

        var article = new Article
        {
            Title = model.Title,
            Content = model.Content,
            AuthorId = CurrentUserId.Value,
            IsPublished = model.IsPublished,
            CoverUrl = model.CoverUrl,
            Slug = model.Slug,
            Type = ArticleType.Post,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Articles.Add(article);
        await _db.SaveChangesAsync();

        var author = await _db.Users.FindAsync(article.AuthorId);
        var response = new ArticleResponse
        {
            Id = article.Id,
            Title = article.Title,
            Content = article.Content,
            Author = author != null ? UserDto.FromUser(author) : new UserDto(),
            CreatedAt = article.CreatedAt,
            UpdatedAt = article.UpdatedAt,
            IsPublished = article.IsPublished,
            CoverUrl = article.CoverUrl,
            CommentCount = 0,
            Type = article.Type,
            Slug = article.Slug
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
        var article = await _db.Articles
            .Include(a => a.Author)
            .Include(a => a.Comments)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (article == null) return NotFound(new MessageResponse("Article not found."));

        if (CurrentUserId == null) return BadRequest(new MessageResponse("User not authenticated."));

        if (article.AuthorId != CurrentUserId.Value) return Forbid();

        if (!string.IsNullOrWhiteSpace(model.Slug) &&
            await _db.Articles.AnyAsync(a => a.Slug == model.Slug && a.Id != id))
            return BadRequest(new MessageResponse("Slug already exists."));

        article.Title = model.Title;
        article.Content = model.Content;
        article.IsPublished = model.IsPublished;
        article.CoverUrl = model.CoverUrl;
        article.Slug = model.Slug;
        article.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ArticleMappingHelper.ToDetailResponse(article));
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
