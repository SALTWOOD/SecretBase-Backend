using Microsoft.AspNetCore.Mvc.Filters;
using System.IdentityModel.Tokens.Jwt;

namespace backend.Filters;

public class AutoRenewTokenAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var expClaim = user.FindFirst(JwtRegisteredClaimNames.Exp);
            if (expClaim != null && long.TryParse(expClaim.Value, out var expUnix))
            {
                var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                var remainingTime = expiresAt - DateTimeOffset.UtcNow;

                if (remainingTime.TotalDays <= 1 && remainingTime.TotalSeconds > 0)
                {
                    // 1. 更新数据库 LastLoginInfo
                     await _userService.UpdateLastLogin(user.GetUserId());

                    // 2. 生成新 Token
                    string newToken = "your_token_generating_logic";

                    // 3. 通过 Set-Cookie 返回
                    httpContext.Response.Cookies.Append("auth_token", newToken, new CookieOptions
                    {
                        HttpOnly = true,    // ⚡ 核心：禁止 JS 读取
                        Secure = true,      // 仅限 HTTPS
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(7) // 设置 Cookie 本身的硬过期时间
                    });
                }
            }
        }
        await next();
    }
}