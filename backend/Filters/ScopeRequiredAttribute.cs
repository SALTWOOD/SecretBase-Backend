namespace backend.Filters;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

/// <summary>
/// OAuth Scope 权限验证特性
/// 用于验证 OAuth Token 是否包含所需的 scope 权限
/// 注意：Cookie Session 认证的用户会跳过 scope 检查（拥有完整权限）
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class ScopeRequiredAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _requiredScopes;

    /// <summary>
    /// 创建 Scope 验证特性
    /// </summary>
    /// <param name="requiredScopes">所需的 scope 列表（满足任一即可）</param>
    public ScopeRequiredAttribute(params string[] requiredScopes)
    {
        _requiredScopes = requiredScopes;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // 未认证用户直接返回（由 [Authorize] 处理）
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // 检查认证类型
        var authType = user.FindFirst("auth_type")?.Value;

        // Cookie Session 认证用户跳过 scope 检查
        if (string.IsNullOrEmpty(authType) || authType != "oauth")
        {
            // 这是 Cookie Session 认证，拥有完整权限
            return;
        }

        // OAuth 认证用户需要检查 scope
        var userScopes = user.FindAll("scope").Select(c => c.Value).ToList();

        // 检查是否拥有所需的任一 scope
        var hasRequiredScope = _requiredScopes.Any(required => 
            userScopes.Contains(required, StringComparer.OrdinalIgnoreCase));

        if (!hasRequiredScope)
        {
            context.Result = new JsonResult(new
            {
                error = "insufficient_scope",
                error_description = $"Token does not have required scope. Required one of: [{string.Join(", ", _requiredScopes)}], but got: [{string.Join(", ", userScopes)}]"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}

/// <summary>
/// 要求拥有所有指定的 scope
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireAllScopesAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _requiredScopes;

    public RequireAllScopesAttribute(params string[] requiredScopes)
    {
        _requiredScopes = requiredScopes;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var authType = user.FindFirst("auth_type")?.Value;

        if (string.IsNullOrEmpty(authType) || authType != "oauth")
        {
            return;
        }

        var userScopes = user.FindAll("scope").Select(c => c.Value).ToList();

        var missingScopes = _requiredScopes.Where(required => 
            !userScopes.Contains(required, StringComparer.OrdinalIgnoreCase)).ToList();

        if (missingScopes.Any())
        {
            context.Result = new JsonResult(new
            {
                error = "insufficient_scope",
                error_description = $"Token is missing required scopes: [{string.Join(", ", missingScopes)}]"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
