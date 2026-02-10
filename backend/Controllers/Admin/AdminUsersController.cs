using backend.Database;
using backend.Database.Entities;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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