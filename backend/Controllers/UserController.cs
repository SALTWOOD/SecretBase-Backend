using backend.Filters;
using backend.Services;
using backend.Tables;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[Route("user")]
public class UserController(BaseServices deps) : BaseApiController(deps)
{
    [Authorize]
    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserTable), StatusCodes.Status200OK)]
    public async Task<IActionResult> Profile()
    {
        return Ok(await CurrentUser);
    }

    [Authorize]
    [HttpPost("profile")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileModel model)
    {
        var user = await _db.Queryable<UserTable>().InSingleAsync(CurrentUserId);

        // check if both old and new passwords are provided or neither
        bool hasNew = !string.IsNullOrWhiteSpace(model.NewPassword);
        bool hasOld = !string.IsNullOrWhiteSpace(model.OldPassword);

        if (hasNew != hasOld) return BadRequest(new MessageResponse("Both old and new passwords must be provided to change the password."));

        // update username if provided and different
        if (!string.IsNullOrEmpty(model.Username) && model.Username != user.Username)
        {
            bool usernameExists = await _db.Queryable<UserTable>()
                .AnyAsync(u => u.Username == model.Username);
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

        await _db.Updateable(user).ExecuteCommandAsync();
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

        var oldAvatar = await _db.Queryable<UserTable>()
            .Where(u => u.Id == CurrentUserId)
            .Select(u => u.Avatar)
            .FirstAsync();

        var fileName = $"{CurrentUserId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";
        var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
        var filePath = Path.Combine(folderPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var avatarUrl = $"/uploads/avatars/{fileName}";
        var success = await _db.Updateable<UserTable>()
            .SetColumns(u => u.Avatar == avatarUrl)
            .Where(u => u.Id == CurrentUserId)
            .ExecuteCommandAsync() > 0;

        if (success && !string.IsNullOrEmpty(oldAvatar) && oldAvatar.StartsWith("/uploads/"))
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