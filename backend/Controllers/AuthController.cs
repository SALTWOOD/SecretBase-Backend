using backend.Filters;
using backend.Models;
using backend.Services;
using backend.Tables;
using backend.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace backend.Controllers;

public class AuthController : BaseApiController
{
    public AuthController(ISqlSugarClient db, ILogger<BaseApiController> logger) : base(db, logger)
    {
    }

    [HttpPost("login")]
    [ValidateCaptcha]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] AuthLoginModel model)
    {
        // query user
        UserTable? user = await _db.Queryable<UserTable>()
            .FirstAsync(u => u.Email == model.Email);

        // check for password
        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            return BadRequest(new MessageResponse("Invalid email or password."));
        }

        // banned
        if (user.IsBanned)
        {
            return StatusCode(403, new MessageResponse("Your account has been banned."));
        }

        int expires = _db.Queryable<SettingTable>().First(s => s.Key == SettingKeys.Site.Security.Jwt.ExpireHours).GetValue<int>();
        await GetService<UserService>().UpdateLastLoginAsync(user, HttpContext);
        await RefreshTokenAsync(user, expires);
        return Ok(new
        {
            message = "Login successful.",
            user,
            expires = DateTime.UtcNow.AddHours(expires)
        });
    }

    [HttpPost("renew")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
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
            user,
            expires = DateTime.UtcNow.AddHours(expires)
        });
    }
}