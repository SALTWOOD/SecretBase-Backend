using Amazon.S3;
using Amazon.S3.Model;
using backend.Services;
using backend.Types.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using backend.Types.Response;
using ImageProxyClient;

namespace backend.Controllers.Admin;

/// <summary>
/// 管理员文件存储管理控制器
/// </summary>
[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("admin/storage/bucket/{bucketName}")]
[Produces(MediaTypeNames.Application.Json)]
public class AdminStorageBucketController : BaseApiController
{
    private readonly IAmazonS3 _s3Client;
    private readonly IImgproxyClient _imgproxyClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminStorageBucketController> _logger;

    public AdminStorageBucketController(
        BaseServices deps,
        IAmazonS3 s3Client,
        IImgproxyClient imgproxyClient,
        IConfiguration configuration,
        ILogger<AdminStorageBucketController> logger) : base(deps)
    {
        _s3Client = s3Client;
        _imgproxyClient = imgproxyClient;
        _configuration = configuration;
        _logger = logger;
    }

    private string BucketName => HttpContext.GetRouteValue("bucketName")?.ToString().ThrowIfNull()!;

    /// <summary>
    /// 列出存储桶中的所有文件
    /// </summary>
    /// <param name="prefix">可选的前缀过滤</param>
    /// <param name="maxKeys">最大返回数量，默认 100</param>
    /// <param name="recursive">是否递归列出所有子目录下的文件。为 true 时展开所有层级，为 false 时仅列出当前目录层级（包含文件夹）。</param>
    [HttpGet("files")]
    [ProducesResponseType<List<S3ObjectResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListFiles(
        [FromQuery] string? prefix = null,
        [FromQuery] int maxKeys = 100,
        [FromQuery] bool recursive = false
    )
    {
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = BucketName,
                Prefix = prefix,
                MaxKeys = maxKeys,
                Delimiter = recursive ? null : "/"
            };

            var response = await _s3Client.ListObjectsV2Async(request);

            var folders = (response.CommonPrefixes ?? Enumerable.Empty<string>())
                .Select(key => new S3ObjectResponse
                {
                    Key = key
                });

            var files = (response.S3Objects ?? Enumerable.Empty<S3Object>())
                .Select(obj => new S3ObjectResponse
                {
                    Key = obj.Key,
                    Size = obj.Size ?? 0,
                    LastModified = obj.LastModified ?? DateTime.MinValue,
                    ETag = obj.ETag,
                    StorageClass = obj.StorageClass
                });

            return Ok(folders.Concat(files));
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to list files from S3 bucket {BucketName}", BucketName);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new MessageResponse { Message = $"Failed to list files: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取文件详情
    /// </summary>
    /// <param name="key">文件键名（URL 编码）</param>
    [HttpGet("file")]
    [ProducesResponseType<S3ObjectMetadataResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFileInfo([FromQuery] string key)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = BucketName,
                Key = key
            };

            var response = await _s3Client.GetObjectMetadataAsync(request);

            var metadata = new S3ObjectMetadataResponse
            {
                Key = key,
                ContentType = response.Headers.ContentType,
                ContentLength = response.ContentLength,
                LastModified = response.LastModified ?? DateTime.MinValue,
                ETag = response.ETag,
                Metadata = response.Metadata.Keys.ToDictionary(k => k, k => response.Metadata[k])
            };

            return Ok(metadata);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound(new MessageResponse { Message = $"File '{key}' not found" });
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to get file info for {Key}", key);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new MessageResponse { Message = $"Failed to get file info: {ex.Message}" });
        }
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    /// <param name="key">文件键名（URL 编码）</param>
    [HttpDelete("file")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteFile([FromQuery] string key)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = BucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request);
            return NoContent();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound(new MessageResponse { Message = $"File '{key}' not found" });
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file {Key}", key);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new MessageResponse { Message = $"Failed to delete file: {ex.Message}" });
        }
    }
    
    /// <summary>
    /// 获取文件缩略图（图片文件）
    /// </summary>
    /// <param name="key">文件键名（URL 编码）</param>
    [HttpGet("thumbnail")]
    [ProducesResponseType<UrlResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetThumbnail([FromQuery] string key)
    {
        var builder = new UriBuilder();
        builder.Scheme = "s3";
        builder.Host = BucketName;
        builder.Path = key;
        var uri = Uri.UnescapeDataString(builder.ToString());
        var url = _imgproxyClient.BuildUrl(uri, options =>
            options.Resize(512, 512, ResizeMode.Fit)
                .Quality(80)
                .Format(ImageFormat.WebP)
        );
        return Ok(new UrlResponse
        {
            Url = url
        });
    }

    /// <summary>
    /// 批量删除文件
    /// </summary>
    /// <param name="keys">文件键名列表</param>
    [HttpDelete("files")]
    [ProducesResponseType<BatchDeleteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteFiles([FromBody] List<string> keys)
    {
        if (keys == null || keys.Count == 0) return BadRequest(new MessageResponse { Message = "No keys provided" });

        try
        {
            var request = new DeleteObjectsRequest
            {
                BucketName = BucketName,
                Objects = keys.Select(k => new KeyVersion { Key = k }).ToList()
            };

            var response = await _s3Client.DeleteObjectsAsync(request);

            var result = new BatchDeleteResponse
            {
                DeletedKeys = response.DeletedObjects.Select(o => o.Key).ToList(),
                FailedKeys = response.DeleteErrors.Select(e => e.Key).ToList()
            };

            return Ok(result);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to batch delete files");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new MessageResponse { Message = $"Failed to delete files: {ex.Message}" });
        }
    }

    /// <summary>
    /// 生成预签名上传 URL
    /// </summary>
    /// <param name="request">上传请求</param>
    [HttpPost("presign-upload")]
    [ProducesResponseType<PresignedUrlResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GeneratePresignedUploadUrl([FromBody] PresignUploadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return BadRequest(new MessageResponse { Message = "Key is required" });

        try
        {
            var presignedRequest = new GetPreSignedUrlRequest
            {
                BucketName = BucketName,
                Key = request.Key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(request.ExpirationMinutes ?? 15),
                ContentType = request.ContentType
            };

            var url = _s3Client.GetPreSignedURL(presignedRequest);

            return Ok(new PresignedUrlResponse
            {
                Url = url,
                Key = request.Key,
                ExpiresAt = DateTime.UtcNow.AddMinutes(request.ExpirationMinutes ?? 15)
            });
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to generate presigned upload URL for {Key}", request.Key);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new MessageResponse { Message = $"Failed to generate presigned URL: {ex.Message}" });
        }
    }

    /// <summary>
    /// 生成预签名下载 URL
    /// </summary>
    /// <param name="key">文件键名</param>
    /// <param name="expirationMinutes">URL有效期（分钟），默认 15 分钟</param>
    [HttpGet("presign-download")]
    [ProducesResponseType<PresignedUrlResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GeneratePresignedDownloadUrl([FromQuery] string key, [FromQuery] int expirationMinutes = 15)
    {
        if (string.IsNullOrWhiteSpace(key)) return BadRequest(new MessageResponse { Message = "Key is required" });

        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = BucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
            };

            var url = _s3Client.GetPreSignedURL(request);

            return Ok(new PresignedUrlResponse
            {
                Url = url,
                Key = key,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes)
            });
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to generate presigned download URL for {Key}", key);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new MessageResponse { Message = $"Failed to generate presigned URL: {ex.Message}" });
        }
    }

    /// <summary>
    /// 复制文件
    /// </summary>
    /// <param name="request">复制请求</param>
    [HttpPost("files/copy")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CopyFile([FromBody] CopyFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceKey) || string.IsNullOrWhiteSpace(request.DestinationKey))
            return BadRequest(new MessageResponse { Message = "SourceKey and DestinationKey are required" });

        try
        {
            var copyRequest = new CopyObjectRequest
            {
                SourceBucket = BucketName,
                SourceKey = request.SourceKey,
                DestinationBucket = BucketName,
                DestinationKey = request.DestinationKey
            };

            await _s3Client.CopyObjectAsync(copyRequest);

            return Ok(new MessageResponse
                { Message = $"File copied from '{request.SourceKey}' to '{request.DestinationKey}'" });
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound(new MessageResponse { Message = $"Source file '{request.SourceKey}' not found" });
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to copy file from {SourceKey} to {DestinationKey}", request.SourceKey,
                request.DestinationKey);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new MessageResponse { Message = $"Failed to copy file: {ex.Message}" });
        }
    }

    /// <summary>
    /// 检查存储桶状态
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType<BucketStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBucketStatus()
    {
        try
        {
            var request = new GetBucketLocationRequest
            {
                BucketName = BucketName
            };

            var locationResponse = await _s3Client.GetBucketLocationAsync(request);

            // 获取存储桶统计信息
            var listRequest = new ListObjectsV2Request
            {
                BucketName = BucketName,
                MaxKeys = 1000
            };

            long totalSize = 0;
            var objectCount = 0;

            ListObjectsV2Response listResponse;
            do
            {
                listResponse = await _s3Client.ListObjectsV2Async(listRequest);
                objectCount += listResponse.KeyCount ?? 0;
                totalSize += listResponse.S3Objects.Sum(o => o.Size ?? 0);
                listRequest.ContinuationToken = listResponse.NextContinuationToken;
            } while (listResponse.IsTruncated == true && objectCount < 10000); // 限制最多统计10000个对象

            return Ok(new BucketStatusResponse
            {
                BucketName = BucketName,
                Region = locationResponse.Location.Value ?? "us-east-1",
                ObjectCount = objectCount,
                TotalSizeBytes = totalSize,
                IsAccessible = true
            });
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to get bucket status for {BucketName}", BucketName);
            return Ok(new BucketStatusResponse
            {
                BucketName = BucketName,
                IsAccessible = false,
                ErrorMessage = ex.Message
            });
        }
    }
}

#region Request/Response Models

public readonly record struct S3ObjectResponse
{
    public required string Key { get; init; }
    public long Size { get; init; }
    public DateTime LastModified { get; init; }
    public string? ETag { get; init; }
    public string? StorageClass { get; init; }
}

public readonly record struct S3ObjectMetadataResponse
{
    public required string Key { get; init; }
    public string? ContentType { get; init; }
    public long ContentLength { get; init; }
    public DateTime LastModified { get; init; }
    public string? ETag { get; init; }
    public Dictionary<string, string> Metadata { get; init; }
}

public readonly record struct PresignedUrlResponse
{
    public required string Url { get; init; }
    public required string Key { get; init; }
    public DateTime ExpiresAt { get; init; }
}

public readonly record struct PresignUploadRequest
{
    public required string Key { get; init; }
    public string? ContentType { get; init; }
    public int? ExpirationMinutes { get; init; }
}

public readonly record struct BatchDeleteResponse
{
    public List<string> DeletedKeys { get; init; }
    public List<string> FailedKeys { get; init; }
}

public readonly record struct CopyFileRequest
{
    public required string SourceKey { get; init; }
    public required string DestinationKey { get; init; }
}

public readonly record struct BucketStatusResponse
{
    public required string BucketName { get; init; }
    public string? Region { get; init; }
    public int ObjectCount { get; init; }
    public long TotalSizeBytes { get; init; }
    public bool IsAccessible { get; init; }
    public string? ErrorMessage { get; init; }
}

public readonly record struct UrlResponse
{
    public string Url { get; init; }
}

#endregion