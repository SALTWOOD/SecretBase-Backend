using backend.Filters;
using backend.Services;
using backend.Tables;
using backend.Types.Request;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[Route("auth")]
public class AuthController(BaseServices deps) : BaseApiController(deps)
{
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

        await UpdateLastLoginAsync(user, HttpContext);
        int expires = await RefreshTokenAsync(user);

        var autoRenew = await _setting.Get<bool>(SettingKeys.Site.Security.Cookie.AutoRenew);

        return Ok(new AuthResponse
        {
            Message = "Login successful.",
            User = user,
            Expires = autoRenew ? DateTime.UtcNow.AddHours(expires) : null
        });
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromServices] SessionService sessionService)
    {
        if (Request.Cookies.TryGetValue(Constants.AUTH_TOKEN_COOKIE_NAME, out var token))
        {
            // revoke token
            await sessionService.RemoveSessionAsync(token);
        }

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
    [ValidateCaptcha]
    [ProducesResponseType(typeof(UserTable), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Register([FromBody] AuthRegisterModel model, [FromServices] SettingService setting)
    {
        bool enabled = await setting.Get<bool>(SettingKeys.Site.User.Registration.Enabled);
        if (!enabled)
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new MessageResponse("User registration is disabled.")
            );


        bool forceInvitation = await setting.Get<bool>(SettingKeys.Site.User.Registration.ForceInvitation);
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
            UsedInviteId = invite?.Id
        };
        await _db.Insertable(newUser).ExecuteCommandAsync();

        await UpdateLastLoginAsync(newUser, HttpContext);
        await RefreshTokenAsync(newUser);
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

        await UpdateLastLoginAsync(user, HttpContext);
        int expires = await RefreshTokenAsync(user);
        return Ok(new
        {
            message = "Token renewed successfully.",
            user,
            expires = DateTime.UtcNow.AddHours(expires)
        });
    }
}