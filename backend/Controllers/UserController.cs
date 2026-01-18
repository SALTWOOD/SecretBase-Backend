using backend.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace backend.Controllers;

public class UserController : BaseApiController
{
    private readonly ISqlSugarClient _db;

    public UserController(ISqlSugarClient db)
    {
        _db = db;
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var user = (await _db.Queryable<UserTable>()
                     .InSingleAsync(CurrentUserId))
                     .ThrowIfNull("User no longer exists.");

        return Ok(user);
    }
}