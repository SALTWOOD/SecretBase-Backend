using backend.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace backend.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("admin/users")]
public class UserAdminController : BaseApiController
{
    public UserAdminController(ISqlSugarClient db, ILogger<BaseApiController> logger)
        : base(db, logger) { }

    [HttpGet]
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

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] bool isBanned)
    {
        var result = await _db.Updateable<UserTable>()
            .SetColumns(it => it.IsBanned == isBanned)
            .Where(it => it.Id == id)
            .ExecuteCommandAsync();

        return result == 0 ? NotFound() : NoContent();
    }
}