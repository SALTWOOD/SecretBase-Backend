using backend.Database;
using backend.Database.Entities;
using backend.Services;
using backend.Types.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static backend.Services.SessionService;

namespace backend.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class Require2FAAttribute : ServiceFilterAttribute
{
    public Require2FAAttribute() : base(typeof(TwoFactorFilter)) { }
}

public class TwoFactorFilter : IAsyncActionFilter
{
    private readonly TwoFactorManager _tfManager;
    private readonly AppDbContext _db;
    private readonly SessionService _session;

    public TwoFactorFilter(TwoFactorManager tfManager, AppDbContext db, SessionService session)
    {
        _tfManager = tfManager;
        _db = db;
        _session = session;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        int id = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("Invalid user identity."));
        var user = await _db.Users
            .FirstOrDefaultAsync(it => it.Id == id);

        if (user != null && user.ForceTwoFactor)
        {
            var authToken = httpContext.Request.Cookies["auth_token"];

            // 检查 token 是否存在
            if (string.IsNullOrEmpty(authToken))
            {
                context.Result = new PreconditionRequiredResult("2fa_challenge");
                return;
            }

            // 获取 token 权限级别
            var permissionLevel = await _session.GetTokenPermissionLevelAsync(authToken);

            // 如果 token 权限级别为 None，要求进行 2FA 验证
            if (permissionLevel == TokenPermissionLevel.None)
            {
                context.Result = new PreconditionRequiredResult("2fa_challenge");
                return;
            }

            // 如果 token 权限级别为 Full，检查是否在 2FA 宽限期内
            if (permissionLevel == TokenPermissionLevel.Full && !await _tfManager.IsApprovedAsync(user.Id, authToken))
            {
                context.Result = new PreconditionRequiredResult("2fa_challenge");
                return;
            }
        }

        await next();
    }
}