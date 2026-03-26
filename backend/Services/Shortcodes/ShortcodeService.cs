using backend.Database;
using backend.Database.Entities;
using backend.Types.Requests;
using backend.Types.Responses;
using Jint.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace backend.Services.Shortcodes;

/// <summary>
/// 简码服务 - 处理简码的业务逻辑
/// </summary>
public class ShortcodeService
{
    private readonly AppDbContext _db;
    private readonly ShortcodeSandbox _sandbox;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ShortcodeService> _logger;

    public ShortcodeService(
        AppDbContext db,
        ShortcodeSandbox sandbox,
        IWebHostEnvironment env,
        ILogger<ShortcodeService> logger)
    {
        _db = db;
        _sandbox = sandbox;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有启用的简码列表（公开）
    /// </summary>
    public async Task<List<ShortcodeListItem>> GetPublicShortcodesAsync()
    {
        return await _db.Shortcodes
            .Where(s => s.IsEnabled)
            .Select(s => new ShortcodeListItem
            {
                Name = s.Name,
                DisplayName = s.DisplayName,
                Description = s.Description
            })
            .ToListAsync();
    }

    /// <summary>
    /// 获取所有简码列表（管理）
    /// </summary>
    public async Task<List<ShortcodeDetail>> GetAllShortcodesAsync()
    {
        return await _db.Shortcodes
            .Include(s => s.CreatedBy)
            .Select(s => new ShortcodeDetail
            {
                Id = s.Id,
                Name = s.Name,
                DisplayName = s.DisplayName,
                Description = s.Description,
                FrontendCode = s.FrontendCode,
                BackendCode = s.BackendCode,
                Permission = s.Permission,
                AllowedRoles = s.AllowedRoles,
                IsEnabled = s.IsEnabled,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                CreatedByUserId = s.CreatedByUserId,
                CreatedByUsername = s.CreatedBy.Username
            })
            .ToListAsync();
    }

    /// <summary>
    /// 获取简码详情
    /// </summary>
    public async Task<ShortcodeDetail?> GetShortcodeByIdAsync(int id)
    {
        var shortcode = await _db.Shortcodes
            .Include(s => s.CreatedBy)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (shortcode == null) return null;

        return new ShortcodeDetail
        {
            Id = shortcode.Id,
            Name = shortcode.Name,
            DisplayName = shortcode.DisplayName,
            Description = shortcode.Description,
            FrontendCode = shortcode.FrontendCode,
            BackendCode = shortcode.BackendCode,
            Permission = shortcode.Permission,
            AllowedRoles = shortcode.AllowedRoles,
            IsEnabled = shortcode.IsEnabled,
            CreatedAt = shortcode.CreatedAt,
            UpdatedAt = shortcode.UpdatedAt,
            CreatedByUserId = shortcode.CreatedByUserId,
            CreatedByUsername = shortcode.CreatedBy.Username
        };
    }

    /// <summary>
    /// 获取前端代码
    /// </summary>
    public async Task<string?> GetFrontendCodeAsync(string name)
    {
        var shortcode = await _db.Shortcodes
            .FirstOrDefaultAsync(s => s.Name == name && s.IsEnabled);

        return shortcode?.FrontendCode;
    }

    /// <summary>
    /// 执行 handler
    /// </summary>
    public async Task<ShortcodeExecutionResult> ExecuteHandlerAsync(
        string name,
        string handlerName,
        JsonElement requestBody,
        Dictionary<string, string> headers,
        Dictionary<string, string> query,
        User? currentUser)
    {
        try
        {
            var shortcode = await _db.Shortcodes
                .FirstOrDefaultAsync(s => s.Name == name);

            if (shortcode == null)
            {
                return new ShortcodeExecutionResult
                {
                    Success = false,
                    Error = new ShortcodeError
                    {
                        Code = "SHORTCODE_NOT_FOUND",
                        Message = $"Shortcode '{name}' not found"
                    }
                };
            }

            if (!shortcode.IsEnabled)
            {
                return new ShortcodeExecutionResult
                {
                    Success = false,
                    Error = new ShortcodeError
                    {
                        Code = "SHORTCODE_DISABLED",
                        Message = $"Shortcode '{name}' is disabled"
                    }
                };
            }

            // 验证权限
            var permissionError = CheckPermission(shortcode, currentUser);
            if (permissionError != null)
            {
                return new ShortcodeExecutionResult
                {
                    Success = false,
                    Error = permissionError
                };
            }

            // 验证 handler 是否存在
            if (!_sandbox.HandlerExists(shortcode.BackendCode, handlerName))
            {
                return new ShortcodeExecutionResult
                {
                    Success = false,
                    Error = new ShortcodeError
                    {
                        Code = "HANDLER_NOT_FOUND",
                        Message = $"Handler '{handlerName}' not found in shortcode '{name}'"
                    }
                };
            }

            // 执行 handler
            var result = await _sandbox.ExecuteHandlerAsync(
                shortcode.BackendCode,
                handlerName,
                requestBody,
                headers,
                query,
                currentUser);

            return new ShortcodeExecutionResult
            {
                Success = true,
                Data = result
            };
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Shortcode handler execution timed out: {Name}/{Handler}", name, handlerName);
            return new ShortcodeExecutionResult
            {
                Success = false,
                Error = new ShortcodeError
                {
                    Code = "TIMEOUT_ERROR",
                    Message = "Handler execution timed out"
                }
            };
        }
        catch (JavaScriptException ex)
        {
            _logger.LogError(ex, "JavaScript error in shortcode handler: {Name}/{Handler}", name, handlerName);
            return new ShortcodeExecutionResult
            {
                Success = false,
                Error = new ShortcodeError
                {
                    Code = "EXECUTION_ERROR",
                    Message = _env.IsDevelopment() ? ex.Message : "An error occurred while executing the handler",
                    Details = _env.IsDevelopment() ? new
                    {
                        stack = ex.StackTrace,
                        line = ex.Location.Start.Line,
                        column = ex.Location.Start.Column,
                        handlerName
                    } : null
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing shortcode handler: {Name}/{Handler}", name, handlerName);
            return new ShortcodeExecutionResult
            {
                Success = false,
                Error = new ShortcodeError
                {
                    Code = "EXECUTION_ERROR",
                    Message = _env.IsDevelopment() ? ex.Message : "An unexpected error occurred"
                }
            };
        }
    }

    /// <summary>
    /// 创建简码
    /// </summary>
    public async Task<ShortcodeDetail> CreateShortcodeAsync(ShortcodeCreateModel model, int userId)
    {
        var shortcode = new Shortcode
        {
            Name = model.Name,
            DisplayName = model.DisplayName,
            Description = model.Description,
            FrontendCode = model.FrontendCode,
            BackendCode = model.BackendCode,
            Permission = model.Permission,
            AllowedRoles = model.AllowedRoles,
            IsEnabled = model.IsEnabled,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Shortcodes.Add(shortcode);
        await _db.SaveChangesAsync();

        return await GetShortcodeByIdAsync(shortcode.Id) ?? throw new InvalidOperationException("Failed to retrieve created shortcode");
    }

    /// <summary>
    /// 更新简码
    /// </summary>
    public async Task<ShortcodeDetail?> UpdateShortcodeAsync(int id, ShortcodeUpdateModel model)
    {
        var shortcode = await _db.Shortcodes.FindAsync(id);
        if (shortcode == null) return null;

        if (model.DisplayName != null)
            shortcode.DisplayName = model.DisplayName;
        if (model.Description != null)
            shortcode.Description = model.Description;
        if (model.FrontendCode != null)
            shortcode.FrontendCode = model.FrontendCode;
        if (model.BackendCode != null)
            shortcode.BackendCode = model.BackendCode;
        if (model.Permission.HasValue)
            shortcode.Permission = model.Permission.Value;
        if (model.AllowedRoles != null)
            shortcode.AllowedRoles = model.AllowedRoles;
        if (model.IsEnabled.HasValue)
            shortcode.IsEnabled = model.IsEnabled.Value;

        shortcode.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await GetShortcodeByIdAsync(id);
    }

    /// <summary>
    /// 更新简码状态
    /// </summary>
    public async Task<ShortcodeDetail?> UpdateShortcodeStatusAsync(int id, bool isEnabled)
    {
        var shortcode = await _db.Shortcodes.FindAsync(id);
        if (shortcode == null) return null;

        shortcode.IsEnabled = isEnabled;
        shortcode.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await GetShortcodeByIdAsync(id);
    }

    /// <summary>
    /// 删除简码
    /// </summary>
    public async Task<bool> DeleteShortcodeAsync(int id)
    {
        var shortcode = await _db.Shortcodes.FindAsync(id);
        if (shortcode == null) return false;

        _db.Shortcodes.Remove(shortcode);
        await _db.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// 检查权限
    /// </summary>
    private ShortcodeError? CheckPermission(Shortcode shortcode, User? currentUser)
    {
        return shortcode.Permission switch
        {
            ShortcodePermission.Anonymous => null,
            ShortcodePermission.Authenticated => currentUser == null
                ? new ShortcodeError { Code = "UNAUTHORIZED", Message = "Authentication required" }
                : null,
            ShortcodePermission.RoleRestricted => currentUser == null
                ? new ShortcodeError { Code = "UNAUTHORIZED", Message = "Authentication required" }
                : shortcode.AllowedRoles == null || !shortcode.AllowedRoles.Contains(currentUser.Role.ToString())
                    ? new ShortcodeError { Code = "FORBIDDEN", Message = "You don't have permission to access this shortcode" }
                    : null,
            _ => new ShortcodeError { Code = "FORBIDDEN", Message = "Unknown permission level" }
        };
    }
}
