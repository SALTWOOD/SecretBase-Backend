using backend.Database.Entities;
using System.ComponentModel.DataAnnotations;

namespace backend.Types.Requests;

/// <summary>
/// 创建简码请求
/// </summary>
public record ShortcodeCreateModel
{
    /// <summary>
    /// 唯一标识符，用于 URL 路由
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 描述信息
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; init; }

    /// <summary>
    /// 前端执行的 JavaScript 代码
    /// </summary>
    [Required]
    public string FrontendCode { get; init; } = string.Empty;

    /// <summary>
    /// 后端执行的 JavaScript 代码
    /// </summary>
    [Required]
    public string BackendCode { get; init; } = string.Empty;

    /// <summary>
    /// 权限级别
    /// </summary>
    public ShortcodePermission Permission { get; init; } = ShortcodePermission.Authenticated;

    /// <summary>
    /// 允许访问的角色列表
    /// </summary>
    public string[]? AllowedRoles { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}

/// <summary>
/// 更新简码请求
/// </summary>
public record ShortcodeUpdateModel
{
    /// <summary>
    /// 显示名称
    /// </summary>
    [MaxLength(200)]
    public string? DisplayName { get; init; }

    /// <summary>
    /// 描述信息
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; init; }

    /// <summary>
    /// 前端执行的 JavaScript 代码
    /// </summary>
    public string? FrontendCode { get; init; }

    /// <summary>
    /// 后端执行的 JavaScript 代码
    /// </summary>
    public string? BackendCode { get; init; }

    /// <summary>
    /// 权限级别
    /// </summary>
    public ShortcodePermission? Permission { get; init; }

    /// <summary>
    /// 允许访问的角色列表
    /// </summary>
    public string[]? AllowedRoles { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool? IsEnabled { get; init; }
}

/// <summary>
/// 更新简码状态请求
/// </summary>
public record ShortcodeStatusModel
{
    /// <summary>
    /// 是否启用
    /// </summary>
    [Required]
    public bool IsEnabled { get; init; }
}