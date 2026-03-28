using Amazon.S3;
using backend.Database;
using backend.Database.Entities;
using backend.Services;
using backend.Types.Requests;
using backend.Types.Responses;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mime;
using FileShare = backend.Database.Entities.FileShare;

namespace backend.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("admin/file-shares")]
[Produces(MediaTypeNames.Application.Json)]
public class AdminFileShareController : BaseApiController
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<AdminFileShareController> _logger;

    public AdminFileShareController(
        BaseServices deps,
        IAmazonS3 s3Client,
        ILogger<AdminFileShareController> logger) : base(deps)
    {
        _s3Client = s3Client;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType<FileShareResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromBody] FileShareCreateRequest request)
    {
        var currentUser = await CurrentUser;
        if (currentUser == null)
        {
            return Unauthorized(new MessageResponse { Message = "User not authenticated" });
        }

        // 生成短 ID
        var shortId = GenerateShortId();

        var fileShare = new FileShare
        {
            ShortId = shortId,
            Bucket = request.Bucket,
            Key = request.Key,
            FileName = request.FileName,
            IsPublic = request.IsPublic,
            IsEnabled = true,
            ExpiresAt = request.ExpiresAt,
            OwnerId = currentUser.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _db.FileShares.AddAsync(fileShare);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { shortId = fileShare.ShortId }, FileShareResponse.FromEntity(fileShare));
    }

    [HttpGet]
    [ProducesResponseType<FileShareListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? bucket = null,
        [FromQuery] bool? isEnabled = null)
    {
        try
        {
            var query = _db.FileShares.AsQueryable();

            if (!string.IsNullOrEmpty(bucket))
            {
                query = query.Where(f => f.Bucket == bucket);
            }

            if (isEnabled.HasValue)
            {
                query = query.Where(f => f.IsEnabled == isEnabled);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var items = await query
                .OrderByDescending(f => f.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new FileShareListResponse
            {
                Items = items.Select(FileShareResponse.FromEntity).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list file shares");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new MessageResponse { Message = $"Failed to list file shares: {ex.Message}" });
        }
    }

    [HttpGet("{shortId}")]
    [ProducesResponseType<FileShareResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string shortId)
    {
        var fileShare = await _db.FileShares
            .FirstOrDefaultAsync(f => f.ShortId == shortId);

        if (fileShare == null)
        {
            return NotFound(new MessageResponse { Message = $"File share '{shortId}' not found" });
        }

        return Ok(FileShareResponse.FromEntity(fileShare));
    }

    [HttpPatch("{shortId}")]
    [ProducesResponseType<FileShareResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string shortId, [FromBody] FileShareUpdateRequest request)
    {
        var fileShare = await _db.FileShares
            .FirstOrDefaultAsync(f => f.ShortId == shortId);

        if (fileShare == null)
        {
            return NotFound(new MessageResponse { Message = $"File share '{shortId}' not found" });
        }

        // 更新字段
        if (request.IsEnabled.HasValue)
        {
            fileShare.IsEnabled = request.IsEnabled.Value;
        }

        if (request.IsPublic.HasValue)
        {
            fileShare.IsPublic = request.IsPublic.Value;
        }

        if (request.ExpiresAt.HasValue)
        {
            fileShare.ExpiresAt = request.ExpiresAt.Value;
        }

        if (request.FileName != null)
        {
            fileShare.FileName = request.FileName;
        }

        await _db.SaveChangesAsync();

        return Ok(FileShareResponse.FromEntity(fileShare));
    }

    /// <summary>
    /// 删除分享链接
    /// </summary>
    /// <param name="shortId">短链接ID</param>
    [HttpDelete("{shortId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string shortId)
    {
        var fileShare = await _db.FileShares
            .FirstOrDefaultAsync(f => f.ShortId == shortId);

        if (fileShare == null)
        {
            return NotFound(new MessageResponse { Message = $"File share '{shortId}' not found" });
        }

        _db.FileShares.Remove(fileShare);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    #region Private Methods

    /// <summary>
    /// 生成短ID
    /// 使用 URL 安全的字符集：A-Za-z0-9_-
    /// 长度：16 位
    /// </summary>
    private static string GenerateShortId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        var random = new char[16];
        var data = new byte[16];

        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(data);
        }

        for (var i = 0; i < 16; i++)
        {
            random[i] = chars[data[i] % chars.Length];
        }

        return new string(random);
    }

    #endregion
}
