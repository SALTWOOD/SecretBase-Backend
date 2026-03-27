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
[Route("admin/storage")]
[Produces(MediaTypeNames.Application.Json)]
public class AdminStorageController : BaseApiController
{
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminStorageBucketController> _logger;
    
    public AdminStorageController(
        BaseServices deps,
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<AdminStorageBucketController> logger) : base(deps)
    {
        _s3Client = s3Client;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 列出所有存储桶
    /// </summary>
    [HttpGet("buckets")]
    [ProducesResponseType<List<BucketResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListBuckets()
    {
        try
        {
            var response = await _s3Client.ListBucketsAsync();
            
            var buckets = response.Buckets.Select(b => new BucketResponse
            {
                Name = b.BucketName,
                CreationDate = b.CreationDate ?? DateTime.MinValue,
            }).ToList();

            return Ok(buckets);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to list S3 buckets");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new MessageResponse { Message = $"Failed to list buckets: {ex.Message}" });
        }
    }

    /// <summary>
    /// 获取存储服务状态
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType<StorageStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStorageStatus()
    {
        try
        {
            var response = await _s3Client.ListBucketsAsync();
            
            return Ok(new StorageStatusResponse
            {
                IsConnected = true,
                BucketCount = response.Buckets.Count,
                OwnerId = response.Owner?.Id,
                OwnerDisplayName = response.Owner?.DisplayName
            });
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to S3 storage");
            return Ok(new StorageStatusResponse
            {
                IsConnected = false,
                ErrorMessage = ex.Message
            });
        }
    }
}

#region Response Models

/// <summary>
/// 存储桶响应
/// </summary>
public class BucketResponse
{
    /// <summary>
    /// 存储桶名称
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreationDate { get; set; }
}

/// <summary>
/// 存储服务状态响应
/// </summary>
public class StorageStatusResponse
{
    /// <summary>
    /// 是否连接成功
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// 存储桶数量
    /// </summary>
    public int BucketCount { get; set; }

    /// <summary>
    /// 所有者 ID
    /// </summary>
    public string? OwnerId { get; set; }

    /// <summary>
    /// 所有者显示名称
    /// </summary>
    public string? OwnerDisplayName { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

#endregion