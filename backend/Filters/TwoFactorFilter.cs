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
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int id))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(it => it.Id == id);

        if (user != null && user.ForceTwoFactor)
        {
            var authToken = httpContext.Request.Cookies["auth_token"];

            // Check if token exists
            if (string.IsNullOrEmpty(authToken))
            {
                context.Result = new PreconditionRequiredResult("2fa_challenge");
                return;
            }

            // Get token permission level
            var permissionLevel = await _session.GetTokenPermissionLevelAsync(authToken);

            // If token permission level is None, require 2FA verification
            if (permissionLevel == TokenPermissionLevel.None)
            {
                context.Result = new PreconditionRequiredResult("2fa_challenge");
                return;
            }

            // If token permission level is Full, check if within 2FA grace period
            if (permissionLevel == TokenPermissionLevel.Full && !await _tfManager.IsApprovedAsync(user.Id, authToken))
            {
                context.Result = new PreconditionRequiredResult("2fa_challenge");
                return;
            }
        }

        await next();
    }
}