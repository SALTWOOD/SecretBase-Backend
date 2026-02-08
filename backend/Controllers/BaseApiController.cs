using backend.Services;
using backend.Tables;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using StackExchange.Redis;
using System.Net.Mime;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class BaseApiController(BaseServices deps) : ControllerBase
{
    protected readonly ISqlSugarClient _db = deps.Database;
    protected readonly IDatabase _redis = deps.Redis.GetDatabase();
    protected readonly SessionService _session = deps.Session;
    protected readonly SettingService _setting = deps.Setting;

    protected int? CurrentUserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int result) ? result : null;

    protected Task<UserTable> CurrentUser => _db.Queryable<UserTable>().FirstAsync(it => it.Id == CurrentUserId).ThrowIfNull();

    protected async ValueTask<int> RefreshTokenAsync(UserTable user)
    {
        (string token, int hours) = await _session.CreateSessionAsync(user, [Permissions.All]);

        Response.Cookies.Append(Constants.AUTH_TOKEN_COOKIE_NAME, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(hours),
            Path = "/"
        });

        return hours;
    }

    public async Task UpdateLastLoginAsync(UserTable user, HttpContext context)
    {
        var lastLogin = new LastLogin
        {
            Time = DateTime.UtcNow,
            IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = context.Request.Headers.UserAgent.ToString()
        };

        await _db.Updateable<UserTable>()
                .SetColumns(u => u.LastLoginInfo == lastLogin)
                .Where(u => u.Id == user.Id)
                .ExecuteCommandAsync();
    }
}