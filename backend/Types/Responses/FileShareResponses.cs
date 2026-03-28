using FileShare = backend.Database.Entities.FileShare;

namespace backend.Types.Responses;

/// <summary>
/// 文件分享响应
/// </summary>
public class FileShareResponse
{
    /// <summary>
    /// 短链接ID
    /// </summary>
    public required string ShortId { get; set; }

    /// <summary>
    /// S3 桶名
    /// </summary>
    public required string Bucket { get; set; }

    /// <summary>
    /// S3 真实路径
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// 原始文件名
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// 是否允许匿名访问
    /// </summary>
    public bool isPublic { get; set; }

    /// <summary>
    /// 此链接是否被启用
    /// </summary>
    public bool isEnabled { get; set; }

    /// <summary>
    /// 上传者 ID
    /// </summary>
    public int OwnerId { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime? expiresAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime createdAt { get; set; }

    /// <summary>
    /// 从 FileShare 实体创建响应对象
    /// </summary>
    /// <param name="entity">FileShare 实体</param>
    /// <returns>FileShareResponse 对象</returns>
    public static FileShareResponse FromEntity(FileShare entity)
    {
        return new FileShareResponse
        {
            ShortId = entity.ShortId,
            Bucket = entity.Bucket,
            Key = entity.Key,
            FileName = entity.FileName,
            isPublic = entity.IsPublic,
            isEnabled = entity.IsEnabled,
            OwnerId = entity.OwnerId,
            expiresAt = entity.ExpiresAt,
            createdAt = entity.CreatedAt
        };
    }
}

/// <summary>
/// 文件分享列表响应
/// </summary>
public class FileShareListResponse
{
    /// <summary>
    /// 分享链接列表
    /// </summary>
    public required List<FileShareResponse> Items { get; set; }

    /// <summary>
    /// 总数量
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 当前页码
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// 每页数量
    /// </summary>
    public int PageSize { get; set; }
}
