using backend.Services;
using backend.Tables;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System.Net.Mime;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class BaseApiController : ControllerBase
{
    protected ISqlSugarClient _db;
    protected ILogger<BaseApiController> _logger;

    public BaseApiController(ISqlSugarClient db, ILogger<BaseApiController> logger)
    {
        this._db = db;
        this._logger = logger;
    }

    protected int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException("Invalid user identity."));

    protected T GetService<T>() where T : notnull => HttpContext.RequestServices.GetRequiredService<T>();

    protected async ValueTask<int> RefreshTokenAsync(UserTable user)
    {
        var _user = GetService<UserService>();
        var _jwt = GetService<SessionService>();

        var expireHours = await _db.Queryable<SettingTable>()
            .FirstAsync(s => s.Key == SettingKeys.Site.Security.Jwt.ExpireHours);

        var hours = expireHours.GetValue<int>();

        string token = await _jwt.CreateSessionAsync(user);

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
}