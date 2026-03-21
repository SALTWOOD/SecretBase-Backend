using backend.Database;
using backend.Database.Entities;
using backend.Filters;
using backend.Services;
using backend.Types.Request;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static backend.Services.SessionService;

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
        // query User
        User? user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == model.Email);

        // check for password
        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            return BadRequest(new MessageResponse("Invalid email or password."));
        }

        var autoRenew = await SettingRegistry.Site.Security.Cookie.AutoRenew;

        if (user.ForceTwoFactor)
        {
            int expires = await RefreshTokenAsync(user, TokenPermissionLevel.None);
            DateTime? expiresValue = autoRenew ? DateTime.UtcNow.AddHours(expires) : null;

            return Ok(new AuthResponse
            {
                Status = "pending",
                Data = new TokenRenewResponse
                {
                    User = user,
                    Expires = expiresValue,
                    Message = "2FA challenge required"
                }
            });
        }
        else
        {
            await UpdateLastLoginAsync(user, HttpContext);
            int expires = await RefreshTokenAsync(user, TokenPermissionLevel.Full);
            DateTime? expiresValue = autoRenew ? DateTime.UtcNow.AddHours(expires) : null;

            return Ok(new AuthResponse
            {
                Status = "success",
                Data = new TokenRenewResponse
                {
                    User = user,
                    Expires = expiresValue,
                    Message = "Login success"
                }
            });
        }
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
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        };
        HttpContext.Response.Cookies.Delete(Constants.AUTH_TOKEN_COOKIE_NAME, options);
        return NoContent();
    }

    [HttpPost("register")]
    [ValidateCaptcha]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Register([FromBody] AuthRegisterModel model)
    {
        bool enabled = await SettingRegistry.Site.User.Registration.Enabled;
        if (!enabled)
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new MessageResponse("User registration is disabled.")
            );


        bool forceInvitation = await SettingRegistry.Site.User.Registration.ForceInvitation;
        Invite? invite = await Utils.GetInvite(_db, model.InviteCode);
        if (forceInvitation && invite == null)
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new MessageResponse("An invitation is required to register.")
            );

        bool emailExists = await _db.Users
            .AnyAsync(u => u.Email == model.Email);
        if (emailExists) return BadRequest(new MessageResponse("Email is already registered."));

        bool usernameExists = await _db.Users
            .AnyAsync(u => u.Username == model.Username);
        if (usernameExists) return BadRequest(new MessageResponse("Username is already taken."));

        // create User
        User newUser = new User
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
        await _db.Users.AddAsync(newUser);
        await _db.SaveChangesAsync();

        await UpdateLastLoginAsync(newUser, HttpContext);
        await RefreshTokenAsync(newUser);
        return Ok(newUser);
    }

    [HttpPost("renew")]
    [Authorize]
    [ProducesResponseType(typeof(TokenRenewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RenewToken()
    {
        var user = await _db.Users
            .Where(u => u.Id == CurrentUserId)
            .Select(u => new User { Id = u.Id, Username = u.Username, Role = u.Role })
            .FirstOrDefaultAsync();
        if (user == null)
        {
            return Unauthorized(new { message = "User not found." });
        }

        await UpdateLastLoginAsync(user, HttpContext);
        int expires = await RefreshTokenAsync(user);
        var autoRenew = await SettingRegistry.Site.Security.Cookie.AutoRenew;
        DateTime? expiresValue = autoRenew ? DateTime.UtcNow.AddHours(expires) : null;

        return Ok(new TokenRenewResponse
        {
            Message = "Token renewed successfully.",
            User = user,
            Expires = expiresValue
        });
    }
}
