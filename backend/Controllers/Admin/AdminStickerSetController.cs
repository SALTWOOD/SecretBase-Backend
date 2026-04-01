using Amazon.S3;
using Amazon.S3.Model;
using backend.Database;
using backend.Database.Entities;
using backend.Services;
using backend.Types.Requests;
using backend.Types.Response;
using backend.Types.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mime;

namespace backend.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("admin/sticker-sets")]
[Produces(MediaTypeNames.Application.Json)]
public class AdminStickerSetController : BaseApiController
{
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminStickerSetController> _logger;

    public AdminStickerSetController(
        BaseServices deps,
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<AdminStickerSetController> logger) : base(deps)
    {
        _s3Client = s3Client;
        _configuration = configuration;
        _logger = logger;
    }

    private string BucketName =>
        _configuration["Features:StickerBucket"]
        ?? throw new InvalidOperationException("S3 bucket for stickers is not configured.");

    [HttpGet]
    [ProducesResponseType<List<StickerSetResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.StickerSets.AsQueryable();

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StickerSetResponse
            {
                Id = s.Id,
                Name = s.Name,
                StickerCount = s.Stickers.Count,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            })
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", totalCount.ToString());

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<StickerSetDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        var stickerSet = await _db.StickerSets
            .Where(s => s.Id == id)
            .Select(s => new StickerSetDetailResponse
            {
                Id = s.Id,
                Name = s.Name,
                Stickers = s.Stickers.Select(st => new StickerResponse
                {
                    Id = st.Id,
                    Name = st.Name
                }).ToList(),
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (stickerSet == null) return NotFound(new MessageResponse("Sticker set not found."));

        return Ok(stickerSet);
    }

    [HttpPost]
    [ProducesResponseType<StickerSetResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateStickerSetRequest request)
    {
        var currentUser = await CurrentUser;
        if (currentUser == null) return Unauthorized(new MessageResponse("User not authenticated."));

        var stickerSet = new StickerSet
        {
            Name = request.Name,
            CreatorId = currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.StickerSets.Add(stickerSet);
        await _db.SaveChangesAsync();

        var response = new StickerSetResponse
        {
            Id = stickerSet.Id,
            Name = stickerSet.Name,
            StickerCount = 0,
            CreatedAt = stickerSet.CreatedAt,
            UpdatedAt = stickerSet.UpdatedAt
        };

        return CreatedAtAction(nameof(Get), new { id = stickerSet.Id }, response);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<StickerSetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateStickerSetRequest request)
    {
        var stickerSet = await _db.StickerSets.FindAsync(id);
        if (stickerSet == null) return NotFound(new MessageResponse("Sticker set not found."));

        if (request.Name != null) stickerSet.Name = request.Name;

        stickerSet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var stickerCount = await _db.Stickers.CountAsync(s => s.StickerSetId == id);

        return Ok(new StickerSetResponse
        {
            Id = stickerSet.Id,
            Name = stickerSet.Name,
            StickerCount = stickerCount,
            CreatedAt = stickerSet.CreatedAt,
            UpdatedAt = stickerSet.UpdatedAt
        });
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var stickerSet = await _db.StickerSets.FindAsync(id);
        if (stickerSet == null) return NotFound(new MessageResponse("Sticker set not found."));

        _db.StickerSets.Remove(stickerSet);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:int}/stickers/presign")]
    [ProducesResponseType<List<PresignedStickerUrl>>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    public IActionResult PresignUpload(int id, [FromBody] PresignStickerUploadRequest request)
    {
        if (request.Items.Count == 0)
            return BadRequest(new MessageResponse("At least one item is required."));

        var results = new List<PresignedStickerUrl>();

        foreach (var item in request.Items)
        {
            var ext = string.IsNullOrEmpty(item.ContentType)
                ? "webp"
                : GetExtensionFromContentType(item.ContentType);
            var key = $"stickers/{id}/{Guid.NewGuid():N}.{ext}";

            try
            {
                var presignedRequest = new GetPreSignedUrlRequest
                {
                    BucketName = BucketName,
                    Key = key,
                    Verb = HttpVerb.PUT,
                    Expires = DateTime.UtcNow.AddMinutes(15),
                    ContentType = item.ContentType
                };

                var url = _s3Client.GetPreSignedURL(presignedRequest);
                results.Add(new PresignedStickerUrl(key, url, DateTime.UtcNow.AddMinutes(15)));
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "Failed to generate presigned upload URL for sticker set {SetId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new MessageResponse($"Failed to generate presigned URL: {ex.Message}"));
            }
        }

        return Ok(results);
    }

    [HttpPost("{id:int}/stickers")]
    [ProducesResponseType<List<StickerResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmUpload(int id, [FromBody] ConfirmStickerUploadRequest request)
    {
        var stickerSet = await _db.StickerSets.FindAsync(id);
        if (stickerSet == null) return NotFound(new MessageResponse("Sticker set not found."));

        if (request.Items.Count == 0)
            return BadRequest(new MessageResponse("At least one item is required."));

        var stickers = new List<Sticker>();

        foreach (var item in request.Items)
        {
            try
            {
                var metadataRequest = new GetObjectMetadataRequest
                {
                    BucketName = BucketName,
                    Key = item.Key
                };

                await _s3Client.GetObjectMetadataAsync(metadataRequest);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return BadRequest(new MessageResponse($"Sticker file '{item.Key}' not found in S3."));
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "Failed to validate sticker file {Key}", item.Key);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new MessageResponse($"Failed to validate sticker file: {ex.Message}"));
            }

            var s3Uri = $"s3://{BucketName}/{item.Key}";
            var sticker = new Sticker
            {
                Name = item.Name,
                Url = s3Uri,
                StickerSetId = id
            };

            _db.Stickers.Add(sticker);
            stickers.Add(sticker);
        }

        stickerSet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var response = stickers.Select(s => new StickerResponse
        {
            Id = s.Id,
            Name = s.Name
        }).ToList();

        return Ok(response);
    }

    [HttpDelete("{id:int}/stickers/{stickerId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSticker(int id, int stickerId)
    {
        var sticker = await _db.Stickers
            .FirstOrDefaultAsync(s => s.Id == stickerId && s.StickerSetId == id);

        if (sticker == null) return NotFound(new MessageResponse("Sticker not found."));

        _db.Stickers.Remove(sticker);

        var stickerSet = await _db.StickerSets.FindAsync(id);
        if (stickerSet != null) stickerSet.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static string GetExtensionFromContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => "png",
            "image/jpeg" => "jpg",
            "image/gif" => "gif",
            "image/webp" => "webp",
            "image/svg+xml" => "svg",
            "image/avif" => "avif",
            _ => "webp"
        };
    }
}
