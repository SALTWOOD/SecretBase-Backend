using backend.Database;
using backend.Database.Entities;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using backend.Database.Models;

namespace backend.Controllers;

[ApiController]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class BaseApiController(BaseServices deps) : ControllerBase
{
    protected readonly Supabase.Client _supa = deps.Supa;
    protected readonly IDatabase _redis = deps.Redis.GetDatabase();
    protected readonly SessionService _session = deps.Session;
    protected readonly SettingService _setting = deps.Setting;

    protected string JwtToken => Request.Cookies[Constants.AUTH_TOKEN_COOKIE_NAME].ThrowIfNull();
    protected async Task<Supabase.Gotrue.User?> GetCurrentUserAsync() => await _supa.Auth.GetUser(JwtToken);

    protected async Task<Guid> GetCurrentUserIdAsync() 
    {
        var user = await _supa.Auth.GetUser(JwtToken);
        return Guid.Parse(user.ThrowIfNull().Id!);
    }

    protected async Task<Profile?> GetCurrentProfileAsync()
    {
        var id = await GetCurrentUserIdAsync();
        return await _supa.From<Profile>().Where(i => i.Id == id).Single();
    }

    protected async ValueTask<int> RefreshTokenAsync(User user, TokenPermissionLevel permissionLevel = TokenPermissionLevel.Full)
    {
        var access = permissionLevel == TokenPermissionLevel.Full ? [Permissions.All] : TokenPermissions.None;
        (string token, int hours) = await _session.CreateSessionAsync(user, access, permissionLevel: permissionLevel);

        Response.Cookies.Append(Constants.AUTH_TOKEN_COOKIE_NAME, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(hours),
            Path = "/"
        });

        return hours;
    }

    public async Task UpdateLastLoginAsync(User user, HttpContext context)
    {
        var lastLogin = new LastLogin
        {
            Time = DateTime.UtcNow,
            IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = context.Request.Headers.UserAgent.ToString()
        };

        var dbUser = await _db.Users.FindAsync(user.Id);
        if (dbUser != null)
        {
            dbUser.LastLoginInfo = lastLogin;
            await _db.SaveChangesAsync();
        }
    }
}
