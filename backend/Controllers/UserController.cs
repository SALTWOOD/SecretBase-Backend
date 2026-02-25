using backend.Database;
using backend.Database.Entities;
using backend.Filters;
using backend.Services;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[Route("user")]
public class UserController(BaseServices deps) : BaseApiController(deps)
{
    [Authorize]
    [ScopeRequired(OAuthScopes.Profile)]
    [HttpGet("profile")]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    public async Task<IActionResult> Profile()
    {
        return Ok(await CurrentUser);
    }

    [Authorize(Policy = "CookieOnly")]  // 密码修改仅限 Cookie Session
    [HttpPost("profile")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileModel model)
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user == null) return BadRequest(new MessageResponse("User not found."));

        // check if both old and new passwords are provided or neither
        bool hasNew = !string.IsNullOrWhiteSpace(model.NewPassword);
        bool hasOld = !string.IsNullOrWhiteSpace(model.OldPassword);

        if (hasNew != hasOld) return BadRequest(new MessageResponse("Both old and new passwords must be provided to change password."));

        // update username if provided and different
        if (!string.IsNullOrEmpty(model.Username) && model.Username != user.Username)
        {
            bool usernameExists = await _db.Users
                .AnyAsync(u => u.Username == model.Username && u.Id != CurrentUserId);
            if (usernameExists) return BadRequest(new MessageResponse("Username is already taken."));

            user.Username = model.Username;
        }

        if (hasNew)
        {
            if (!BCrypt.Net.BCrypt.Verify(model.OldPassword, user.PasswordHash))
            {
                return BadRequest(new MessageResponse("Entered wrong old password."));
            }
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        }

        await _db.SaveChangesAsync();
        return Ok(new MessageResponse("Profile updated."));
    }

    [HttpPost("avatar")]
    [Authorize]
    [Disabled(true)]
    [ProducesResponseType(typeof(AvatarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAvatar(IFormFile file)
    {
        if (file == null)
            return BadRequest(new MessageResponse { Message = "No file provided." });
        if (file.Length > 2 * 1024 * 1024)
            return BadRequest($"Image too big. Expected lower than 2097152 bytes, but found ${file.Length} bytes.");

        var extension = Path.GetExtension(file.FileName).ToLower();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExtensions.Contains(extension)) return BadRequest(new MessageResponse { Message = "Unsupported image format." });

        try
        {
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(file.OpenReadStream());
        }
        catch { return BadRequest(new MessageResponse("Invalid image file.")); }

        var oldAvatar = await _db.Users
            .Where(u => u.Id == CurrentUserId)
            .Select(u => u.Avatar)
            .FirstOrDefaultAsync();

        var fileName = $"{CurrentUserId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";
        var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
        var filePath = Path.Combine(folderPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var avatarUrl = $"/uploads/avatars/{fileName}";
        var dbUser = await _db.Users.FindAsync(CurrentUserId);
        if (dbUser != null)
        {
            dbUser.Avatar = avatarUrl;
            await _db.SaveChangesAsync();
        }

        if (!string.IsNullOrEmpty(oldAvatar) && oldAvatar.StartsWith("/uploads/"))
        {
            var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", oldAvatar.TrimStart('/'));
            if (System.IO.File.Exists(oldFilePath))
            {
                System.IO.File.Delete(oldFilePath);
            }
        }

        return Ok(new AvatarResponse(avatarUrl));
    }
}

public class UpdateProfileModel
{
    public string? NewPassword { get; set; }
    public string? Username { get; set; }
    public string? OldPassword { get; set; }
}
