using backend.Database;
using backend.Database.Entities;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("admin/users")]
public class UserAdminController(BaseServices deps) : BaseApiController(deps)
{
    [HttpGet]
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
    public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] bool isBanned)
    {
        // 获取当前用户信息
        var currentUserIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (currentUserIdClaim == null || !int.TryParse(currentUserIdClaim.Value, out var currentUserId))
        {
            return Unauthorized(new { message = "Invalid user identity" });
        }

        // 获取当前用户的角色
        var currentUser = await _db.Users
            .Where(u => u.Id == currentUserId)
            .Select(u => new { u.Role })
            .FirstOrDefaultAsync();

        if (currentUser == null)
        {
            return Unauthorized(new { message = "Current user not found" });
        }

        // 获取目标用户信息
        var targetUser = await _db.Users
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync();

        if (targetUser == null)
        {
            return NotFound(new { message = $"User with ID {id} not found" });
        }

        // 检查权限：只能操作等级低于自己的用户
        if (targetUser.Role >= currentUser.Role)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Cannot modify users with equal or higher role level" });
        }

        var rowsAffected = await _db.Users
            .Where(u => u.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.IsBanned, isBanned)
            );

        if (rowsAffected == 0)
            return NotFound(new { message = $"User with ID {id} not found" });

        return NoContent();
    }
}