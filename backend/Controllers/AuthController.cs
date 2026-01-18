using backend.Filters;
using backend.Models;
using backend.Services;
using backend.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace backend.Controllers;

public class AuthController : BaseApiController
{
    private readonly ISqlSugarClient _db;

    public AuthController(ISqlSugarClient db)
    {
        _db = db;
    }

    [HttpPost]
    [ValidateCaptcha]
    public async Task<IActionResult> Login([FromBody] AuthLoginModel model)
    {
        // query user
        UserTable? user = await _db.Queryable<UserTable>()
            .FirstAsync(u => u.Email == model.Email);

        // check for password
        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            return BadRequest(new { message = "Invalid email or password." });
        }

        // banned
        if (user.IsBanned)
        {
            return StatusCode(403, new { message = "Your account has been banned." });
        }

        int expires = _db.Queryable<SettingTable>().First(s => s.Key == SettingKeys.Site.Security.Jwt.ExpireHours).GetValue<int>();
        await GetService<UserService>().UpdateLastLoginAsync(user, HttpContext);
        await RefreshTokenAsync(user, expires);
        return Ok(new
        {
            message = "Login successful.",
            user = new { user.Username, user.Role },
            expires = DateTime.UtcNow.AddHours(expires)
        });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> RenewToken()
    {
        var user = await _db.Queryable<UserTable>()
            .Where(u => u.Id == CurrentUserId)
            .Select(u => new UserTable { Id = u.Id, Username = u.Username, Role = u.Role })
            .FirstAsync();
        if (user == null)
        {
            return Unauthorized(new { message = "User not found." });
        }

        int expires = _db.Queryable<SettingTable>().First(s => s.Key == SettingKeys.Site.Security.Jwt.ExpireHours).GetValue<int>();
        await GetService<UserService>().UpdateLastLoginAsync(user, HttpContext);
        await RefreshTokenAsync(user, expires);
        return Ok(new
        {
            message = "Token renewed successfully.",
            user = new { user.Username, user.Role },
            expires = DateTime.UtcNow.AddHours(expires)
        });
    }
}