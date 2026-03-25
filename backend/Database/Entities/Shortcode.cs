using System.ComponentModel.DataAnnotations;

namespace backend.Database.Entities;

/// <summary>
/// 简码实体 - 存储可动态执行的 JavaScript 代码
/// </summary>
public class Shortcode
{
    public int Id { get; set; }

    /// <summary>
    /// 唯一标识符，用于 URL 路由
    /// </summary>
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 描述信息
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// 前端执行的 JavaScript 代码
    /// </summary>
    public string FrontendCode { get; set; } = string.Empty;

    /// <summary>
    /// 后端执行的 JavaScript 代码（包含 handler 函数）
    /// </summary>
    public string BackendCode { get; set; } = string.Empty;

    /// <summary>
    /// 权限级别
    /// </summary>
    public ShortcodePermission Permission { get; set; } = ShortcodePermission.Authenticated;

    /// <summary>
    /// 允许访问的角色列表（仅当 Permission = RoleRestricted 时有效）
    /// </summary>
    public string[]? AllowedRoles { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 创建者（管理员）
    /// </summary>
    public int CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;
}

/// <summary>
/// 简码权限级别
/// </summary>
public enum ShortcodePermission
{
    /// <summary>
    /// 任何人都可以访问（包括未登录用户）
    /// </summary>
    Anonymous = 0,

    /// <summary>
    /// 需要登录才能访问
    /// </summary>
    Authenticated = 1,

    /// <summary>
    /// 需要特定角色才能访问
    /// </summary>
    RoleRestricted = 2
}
