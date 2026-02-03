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

        await GetService<UserService>().UpdateLastLoginAsync(user, HttpContext);
        int expires = await RefreshTokenAsync(user);
        return Ok(new
        {
            message = "Login successful.",
            user,
            expires = DateTime.UtcNow.AddHours(expires)
        });
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        CookieOptions options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        };
        HttpContext.Response.Cookies.Delete(Constants.AUTH_TOKEN_COOKIE_NAME, options);
        return NoContent();
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(UserTable), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Register([FromBody] AuthRegisterModel model)
    {
        bool enabled = await GetService<SettingService>().Get<bool>(SettingKeys.Site.User.Registration.Enabled);
        if (!enabled)
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new MessageResponse("User registration is disabled.")
            );


        bool forceInvitation = await GetService<SettingService>().Get<bool>(SettingKeys.Site.User.Registration.ForceInvitation);
        InviteTable? invite = await Utils.GetInvite(_db, model.InviteCode);
        if (forceInvitation && invite == null)
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new MessageResponse("An invitation is required to register.")
            );

        bool emailExists = await _db.Queryable<UserTable>()
            .AnyAsync(u => u.Email == model.Email);
        if (emailExists) return BadRequest(new MessageResponse("Email is already registered."));

        bool usernameExists = await _db.Queryable<UserTable>()
            .AnyAsync(u => u.Username == model.Username);
        if (usernameExists) return BadRequest(new MessageResponse("Username is already taken."));

        // create user
        UserTable newUser = new UserTable
        {
            Username = model.Username,
            Email = model.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            Role = UserRole.User,
            IsBanned = false,
            RegisterTime = DateTime.UtcNow,
            Avatar = Constants.DEFAULT_AVATAR_URL,
        };
        await _db.Insertable(newUser).ExecuteCommandAsync();
        return Ok(newUser);
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

        await GetService<UserService>().UpdateLastLoginAsync(user, HttpContext);
        int expires = await RefreshTokenAsync(user);
        return Ok(new
        {
            message = "Token renewed successfully.",
            user,
            expires = DateTime.UtcNow.AddHours(expires)
        });
    }
}