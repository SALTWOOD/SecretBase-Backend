using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using backend.Database;
using backend.Database.Entities;
using backend.Services;
using backend.Types.Responses;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using System.Text.Encodings.Web;

namespace backend.Controllers;

[ApiController]
[Route("shared/{shortId}")]
public class FileShareController : BaseApiController
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<FileShareController> _logger;

    public FileShareController(
        BaseServices deps,
        IAmazonS3 s3Client,
        ILogger<FileShareController> logger) : base(deps)
    {
        _s3Client = s3Client;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AccessFile(string shortId)
    {
        var fileShare = await _db.FileShares.FindAsync(shortId);

        if (fileShare == null)
        {
            return NotFound(new MessageResponse { Message = "Share link not found" });
        }

        // 检查是否启用
        if (!fileShare.IsEnabled)
        {
            return StatusCode(StatusCodes.Status410Gone, 
                new MessageResponse { Message = "This share link has been disabled" });
        }

        // 检查是否过期
        if (fileShare.ExpiresAt.HasValue && fileShare.ExpiresAt.Value < DateTime.UtcNow)
        {
            return StatusCode(StatusCodes.Status410Gone, 
                new MessageResponse { Message = "This share link has expired" });
        }

        // 检查是否需要登录
        if (!fileShare.IsPublic)
        {
            var currentUser = await CurrentUser;
            if (currentUser == null)
            {
                return Unauthorized(new MessageResponse { Message = "Authentication required to access this file" });
            }
        }

        // 生成 Presigned URL（5 分钟）
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = fileShare.Bucket,
                Key = fileShare.Key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(5),
                ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(fileShare.FileName)}"
                }
            };

            var presignedUrl = await _s3Client.GetPreSignedURLAsync(request);

            return Redirect(presignedUrl);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to generate presigned URL for file share {ShortId}", shortId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new MessageResponse { Message = "Failed to generate download URL" });
        }
    }
}
