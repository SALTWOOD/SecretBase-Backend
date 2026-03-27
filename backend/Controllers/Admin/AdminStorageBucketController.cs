using Amazon.S3;
using Amazon.S3.Model;
using backend.Services;
using backend.Types.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using backend.Types.Response;

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
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminStorageBucketController> _logger;

    public AdminStorageBucketController(
        BaseServices deps,
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<AdminStorageBucketController> logger) : base(deps)
    {
        _s3Client = s3Client;
        _configuration = configuration;
        _logger = logger;
    }

    private string BucketName => HttpContext.GetRouteValue("bucketName")?.ToString().ThrowIfNull()!;

    /// <summary>
    /// 列出存储桶中的所有文件
    /// </summary>
    /// <param name="prefix">可选的前缀过滤</param>
    /// <param name="maxKeys">最大返回数量，默认 100</param>
    [HttpGet("files")]
    [ProducesResponseType<List<S3ObjectResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListFiles([FromQuery] string? prefix = null, [FromQuery] int maxKeys = 100)
    {
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = BucketName,
                Prefix = prefix,
                MaxKeys = maxKeys
            };

            var response = await _s3Client.ListObjectsV2Async(request);
            
            var files = response.S3Objects.Select(obj => new S3ObjectResponse
            {
                Key = obj.Key,
                Size = obj.Size ?? 0,
                LastModified = obj.LastModified ?? DateTime.MinValue,
                ETag = obj.ETag,
                StorageClass = obj.StorageClass
            }).ToList();

            return Ok(files);
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
    [HttpGet("files/{*key}")]
    [ProducesResponseType<S3ObjectMetadataResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFileInfo(string key)
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
    [HttpDelete("files/{*key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteFile(string key)
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
        if (keys == null || keys.Count == 0)
        {
            return BadRequest(new MessageResponse { Message = "No keys provided" });
        }

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
        {
            return BadRequest(new MessageResponse { Message = "Key is required" });
        }

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
    [HttpGet("presign-download/{*key}")]
    [ProducesResponseType<PresignedUrlResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GeneratePresignedDownloadUrl(string key, [FromQuery] int expirationMinutes = 15)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(new MessageResponse { Message = "Key is required" });
        }

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
        {
            return BadRequest(new MessageResponse { Message = "SourceKey and DestinationKey are required" });
        }

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

            return Ok(new MessageResponse { Message = $"File copied from '{request.SourceKey}' to '{request.DestinationKey}'" });
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound(new MessageResponse { Message = $"Source file '{request.SourceKey}' not found" });
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to copy file from {SourceKey} to {DestinationKey}", request.SourceKey, request.DestinationKey);
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
            int objectCount = 0;

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

/// <summary>
/// S3 对象响应
/// </summary>
public class S3ObjectResponse
{
    /// <summary>
    /// 对象键名
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// ETag
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// 存储类型
    /// </summary>
    public string? StorageClass { get; set; }
}

/// <summary>
/// S3 对象元数据响应
/// </summary>
public class S3ObjectMetadataResponse
{
    /// <summary>
    /// 对象键名
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// 内容类型
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// 内容长度
    /// </summary>
    public long ContentLength { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// ETag
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// 用户自定义元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// 预签名URL响应
/// </summary>
public class PresignedUrlResponse
{
    /// <summary>
    /// 预签名URL
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// 对象键名
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// 预签名上传请求
/// </summary>
public class PresignUploadRequest
{
    /// <summary>
    /// 对象键名
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// 内容类型
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// URL有效期（分钟），默认15分钟
    /// </summary>
    public int? ExpirationMinutes { get; set; }
}

/// <summary>
/// 批量删除响应
/// </summary>
public class BatchDeleteResponse
{
    /// <summary>
    /// 已删除的键名列表
    /// </summary>
    public List<string> DeletedKeys { get; set; } = new();

    /// <summary>
    /// 删除失败的键名列表
    /// </summary>
    public List<string> FailedKeys { get; set; } = new();
}

/// <summary>
/// 复制文件请求
/// </summary>
public class CopyFileRequest
{
    /// <summary>
    /// 源文件键名
    /// </summary>
    public required string SourceKey { get; set; }

    /// <summary>
    /// 目标文件键名
    /// </summary>
    public required string DestinationKey { get; set; }
}

/// <summary>
/// 存储桶状态响应
/// </summary>
public class BucketStatusResponse
{
    /// <summary>
    /// 存储桶名称
    /// </summary>
    public required string BucketName { get; set; }

    /// <summary>
    /// 区域
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// 对象数量
    /// </summary>
    public int ObjectCount { get; set; }

    /// <summary>
    /// 总大小（字节）
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// 是否可访问
    /// </summary>
    public bool IsAccessible { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

#endregion
