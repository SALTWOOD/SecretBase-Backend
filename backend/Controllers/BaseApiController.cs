using backend.Services;
using backend.Tables;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BaseApiController : ControllerBase
    {
        protected int CurrentUserId =>
            int.Parse(User.FindFirst("id")?.Value
            ?? throw new UnauthorizedAccessException("Invalid user identity."));

        protected T GetService<T>() where T : notnull => HttpContext.RequestServices.GetRequiredService<T>();

        protected async ValueTask RefreshTokenAsync(UserTable user, int expires = 24)
        {
            var _user = GetService<UserService>();
            var _jwt = GetService<JwtService>();

            string token = await _jwt.IssueJwtToken(user);

            Response.Cookies.Append(Constants.AUTH_TOKEN_COOKIE_NAME, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(expires)
            });
        }
    }
}