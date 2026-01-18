using backend.Services;
using backend.Tables;
using Microsoft.AspNetCore.Mvc.Filters;
using SqlSugar;
using System.IdentityModel.Tokens.Jwt;

namespace backend.Filters;

public class AutoRenewTokenAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var userClaims = httpContext.User;

        if (userClaims.Identity?.IsAuthenticated == true)
        {
            var expClaim = userClaims.FindFirst(JwtRegisteredClaimNames.Exp);
            if (expClaim != null && long.TryParse(expClaim.Value, out var expUnix))
            {
                var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                var remainingTime = expiresAt - DateTimeOffset.UtcNow;

                if (remainingTime.TotalDays <= 1 && remainingTime.TotalSeconds > 0)
                {
                    var services = httpContext.RequestServices;
                    var db = services.GetRequiredService<ISqlSugarClient>();
                    var userService = services.GetRequiredService<UserService>();

                    var userIdStr = userClaims.FindFirst("id")?.Value;
                    if (int.TryParse(userIdStr, out int userId))
                    {
                        var user = await db.Queryable<UserTable>().InSingleAsync(userId);
                        if (user != null)
                        {
                            await userService.UpdateLastLoginAsync(user, httpContext);

                            var expireHours = await db.Queryable<SettingTable>()
                                .Where(s => s.Key == SettingKeys.Site.Security.Jwt.ExpireHours)
                                .Select(s => s.Value)
                                .FirstAsync();

                            int hours = int.TryParse(expireHours, out var h) ? h : 24;

                            var jwtService = services.GetRequiredService<JwtService>();
                            string newToken = await jwtService.IssueJwtToken(user);

                            httpContext.Response.Cookies.Append(Constants.AUTH_TOKEN_COOKIE_NAME, newToken, new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = true,
                                SameSite = SameSiteMode.Lax,
                                Expires = DateTimeOffset.UtcNow.AddHours(hours)
                            });
                        }
                    }
                }
            }
        }

        await next();
    }
}