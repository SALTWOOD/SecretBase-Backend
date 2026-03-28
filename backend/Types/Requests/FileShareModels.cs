using System.ComponentModel.DataAnnotations;
using backend.Database.Entities;

namespace backend.Types.Requests;

/// <summary>
/// 创建分享链接请求
/// </summary>
public class FileShareCreateRequest
{
    /// <summary>
    /// S3 桶名
    /// </summary>
    [Required]
    public string Bucket { get; set; } = string.Empty;

    /// <summary>
    /// S3 键名（如 uploads/2026/03/secret.pdf）
    /// </summary>
    [Required]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 原始文件名（用于重定向时设置 Content-disposition）
    /// </summary>
    [Required]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 是否允许匿名访问
    /// </summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>
    /// 过期时间（可为空表示永不过期）
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// 更新分享链接请求
/// </summary>
public class FileShareUpdateRequest
{
    /// <summary>
    /// 此链接是否被启用（即允许访问）
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// 是否允许匿名访问
    /// </summary>
    public bool? IsPublic { get; set; }

    /// <summary>
    /// 过期时间（可为空表示永不过期）
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 原始文件名
    /// </summary>
    public string? FileName { get; set; }
}
