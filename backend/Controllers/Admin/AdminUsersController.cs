using backend.Services;
using backend.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace backend.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("admin/users")]
public class UserAdminController(BaseServices deps) : BaseApiController(deps)
{
    /// <summary>
    /// Get all registered invites
    /// </summary>
    [HttpGet]
    [ProducesResponseType<List<UserTable>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size > 100) size = 100;

        RefAsync<int> totalCount = 0;

        var users = await _db.Queryable<UserTable>()
            .OrderByDescending(it => it.Id)
            .ToPageListAsync(page, size, totalCount);

        Response.Headers.Append("X-Total-Count", totalCount.ToString());

        return Ok(users);
    }

    /// <summary>
    /// Update user status (e.g., Ban/Unban)
    /// </summary>
    [HttpPut("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] bool isBanned)
    {
        var target = new UserTable
        {
            Id = id,
            IsBanned = isBanned
        };

        var result = await _db.Updateable(target)
            .UpdateColumns(it => new { it.IsBanned })
            .ExecuteCommandAsync();

        if (result == 0) return NotFound(new { message = $"User with ID {id} not found" });

        return NoContent();
    }
}