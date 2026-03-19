using backend.Database;
using backend.Database.Models;
using backend.Filters;
using backend.Services;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Supabase.Gotrue;
using User = backend.Database.Entities.User;

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
        return Ok(await GetCurrentProfileAsync());
    }

    [Authorize(Policy = "CookieOnly")]
    [HttpPost("profile")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileModel model)
    {
        var user = await GetCurrentUserAsync();
        var profile = await GetCurrentProfileAsync();
        var id = Guid.Parse(user.Id);

        if (user == null) return Unauthorized(new MessageResponse("User not found."));

        bool hasNew = !string.IsNullOrWhiteSpace(model.NewPassword);
        bool hasOld = !string.IsNullOrWhiteSpace(model.OldPassword);

        if (hasNew != hasOld)
            return BadRequest(new MessageResponse("Both old and new passwords must be provided."));

        if (hasNew)
        {
            var attrs = new AdminUserAttributes { Password = model.NewPassword };
            var updateRes = await _supa.Auth.Update(attrs);
            if (updateRes == null) return BadRequest(new MessageResponse("Failed to update password."));
        }

        if (!string.IsNullOrEmpty(model.Username))
        {
            var existing = await _supa
                .From<Profile>()
                .Where(u => u.Username == model.Username && u.Id != id)
                .Get();

            if (existing.Models.Any())
                return BadRequest(new MessageResponse("Username is already taken."));

            await _supa
                .From<Profile>()
                .Where(x => x.Id == id)
                .Set(x => x.Username, model.Username)
                .Update();
        }

        return Ok(new MessageResponse("Profile updated."));
    }

    [HttpPost("avatar")]
    [Authorize]
    [Disabled(true)]
    [ProducesResponseType(typeof(AvatarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAvatar(IFormFile? file)
    {
        const string bucket = "user_avatar";
        
        // check file
        if (file == null || file.Length == 0)
            return BadRequest(new MessageResponse("No file provided."));

        // check file size
        if (file.Length > 2 * 1024 * 1024)
            return BadRequest(new MessageResponse("Original image is too large."));

        // get filename
        var userId = await GetCurrentUserIdAsync();
        var fileName = $"{userId}.webp";

        using var outStream = new MemoryStream();
        try
        {
            using (var image = await Image.LoadAsync(file.OpenReadStream()))
            {
                image.Mutate(x => x
                    .Resize(new ResizeOptions { Size = new Size(512, 512), Mode = ResizeMode.Max })
                );

                var encoder = new WebpEncoder
                {
                    Quality = 75,
                    FileFormat = WebpFileFormatType.Lossy
                };

                // save as webp
                await image.SaveAsWebpAsync(outStream, encoder);
            }
        }
        catch
        {
            return BadRequest(new MessageResponse("Invalid image file."));
        }

        // remove old avatar
        var profile = await GetCurrentProfileAsync();
        if (!string.IsNullOrEmpty(profile?.Avatar))
        {
            var oldFileName = Path.GetFileName(profile.Avatar);
            await _supa.Storage.From(bucket).Remove(new List<string> { oldFileName });
        }

        var fileBytes = outStream.ToArray();
        await _supa.Storage
            .From(bucket)
            .Upload(fileBytes, fileName, new Supabase.Storage.FileOptions { ContentType = "image/webp" });

        var publicUrl = _supa.Storage.From(bucket).GetPublicUrl(fileName);

        await _supa.From<Profile>()
            .Where(x => x.Id == userId)
            .Set(x => x.Avatar, publicUrl)
            .Update();

        return Ok(new { avatarUrl = publicUrl });
    }
}

public class UpdateProfileModel
{
    public string? NewPassword { get; set; }
    public string? Username { get; set; }
    public string? OldPassword { get; set; }
}