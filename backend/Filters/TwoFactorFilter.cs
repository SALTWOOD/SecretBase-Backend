using backend.Services;
using backend.Tables;
using backend.Types.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SqlSugar;
using System.Security.Claims;
using static backend.Tables.SettingKeys.Site;

namespace backend.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class Require2FAAttribute : ServiceFilterAttribute
{
    public Require2FAAttribute() : base(typeof(TwoFactorFilter)) { }
}

public class TwoFactorFilter : IAsyncActionFilter
{
    private readonly TwoFactorManager _tfManager;
    private readonly ISqlSugarClient _db;

    public TwoFactorFilter(TwoFactorManager tfManager, ISqlSugarClient db)
    {
        _tfManager = tfManager;
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        int id = int.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("Invalid user identity."));
        var user = await _db.Queryable<UserTable>().FirstAsync(it => it.Id == id);

        if (user != null && user.ForceTwoFactor)
        {
            var authToken = httpContext.Request.Cookies["auth_token"];

            if (string.IsNullOrEmpty(authToken) || !await _tfManager.IsApprovedAsync(user.Id, authToken))
            {
                context.Result = new PreconditionRequiredResult("2fa_challenge");
                return;
            }
        }

        await next();
    }
}