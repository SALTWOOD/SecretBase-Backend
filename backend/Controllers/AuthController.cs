using backend.Filters;
using backend.Models;
using backend.Services;
using backend.Tables;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace backend.Controllers;

public class AuthController : BaseApiController
{
    private readonly ISqlSugarClient _db;
    private readonly JwtService _jwt;
    private ICapValidateService _cap;

    public AuthController(ISqlSugarClient db, JwtService jwt, ICapValidateService cap)
    {
        _db = db;
        _jwt = jwt;
        _cap = cap;
    }

    [HttpPost("login")]
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

        // issue token
        string token = await _jwt.IssueJwtToken(user);
        int expireHours = _db.Queryable<SettingTable>().First(s => s.Key == "site.security.jwt.expire_hours").GetValue<int>();

        // write cookie
        Response.Cookies.Append("auth_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(expireHours)
        });

        return Ok(new
        {
            message = "Login successful.",
            user = new { user.Username, user.Role }
        });
    }
}