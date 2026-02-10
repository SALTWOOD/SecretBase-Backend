using backend.Database;
using backend.Database.Entities;
using backend.Services;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Controllers.Admin;

public readonly record struct UpdateUserStatusBody(
    bool IsBanned
);

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("admin/users")]
public class UserAdminController(BaseServices deps) : BaseApiController(deps)
{
    [HttpGet]
    [ProducesResponseType(typeof(List<User>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<User>>> GetUsers([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);

        var totalCount = await _db.Users.CountAsync();

        var users = await _db.Users
            .OrderByDescending(u => u.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", totalCount.ToString());

        return Ok(users);
    }

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusBody body)
    {
        // Get current user information
        var user = await CurrentUser;

        if (user == null)
        {
            return Unauthorized(new { message = "Current user not found" });
        }

        // Get target user information
        var targetUser = await _db.Users
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync();

        if (targetUser == null)
        {
            return NotFound(new { message = $"User with ID {id} not found" });
        }

        if (user.Id == targetUser.Id)
        {
            return BadRequest(new MessageResponse { Message = "Cannot ban yourself!" });
        }

        // Check permission: can only operate on users with lower role level than oneself
        if (targetUser.Role >= user.Role)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new MessageResponse { Message = "Cannot modify users with equal or higher role level" });
        }

        var rowsAffected = await _db.Users
            .Where(u => u.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.IsBanned, body.IsBanned)
            );

        if (rowsAffected == 0)
            return NotFound(new { message = $"User with ID {id} not found" });

        return NoContent();
    }
}