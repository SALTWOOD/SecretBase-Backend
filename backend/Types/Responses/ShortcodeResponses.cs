using backend.Database.Entities;

namespace backend.Types.Responses;

/// <summary>
/// 简码列表项（公开）
/// </summary>
public record ShortcodeListItem
{
    /// <summary>
    /// 唯一标识符
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 描述信息
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// 简码详情（管理）
/// </summary>
public record ShortcodeDetail
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string FrontendCode { get; init; } = string.Empty;
    public string BackendCode { get; init; } = string.Empty;
    public ShortcodePermission Permission { get; init; }
    public string[]? AllowedRoles { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int CreatedByUserId { get; init; }
    public string CreatedByUsername { get; init; } = string.Empty;
}

/// <summary>
/// 简码执行结果
/// </summary>
public record ShortcodeExecutionResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 返回数据（成功时）
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// 错误信息（失败时）
    /// </summary>
    public ShortcodeError? Error { get; init; }
}

/// <summary>
/// 简码执行错误
/// </summary>
public record ShortcodeError
{
    /// <summary>
    /// 错误码
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 错误详情（仅开发环境）
    /// </summary>
    public object? Details { get; init; }
}

/// <summary>
/// 简码列表响应
/// </summary>
public record ShortcodeListResponse
{
    public List<ShortcodeListItem> Shortcodes { get; init; } = [];
}