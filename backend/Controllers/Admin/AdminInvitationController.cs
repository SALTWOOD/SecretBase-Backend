using backend.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System.Security.Cryptography;

namespace backend.Controllers.Admin;

public record CreateInvitationRequest(int Uses, int HoursValid);
public record UpdateInvitationRequest(bool? IsDisabled, int? Uses, int? HoursValid);

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("admin/invitations")]
public class InvitationAdminController : BaseApiController
{
    public InvitationAdminController(ISqlSugarClient db, ILogger<BaseApiController> logger)
        : base(db, logger) { }

    private static string GenerateSecureCode()
    {
        const string chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var result = RandomNumberGenerator.GetString(chars, 20);
        return string.Join("-", Enumerable.Range(0, 4).Select(i => result.Substring(i * 5, 5)));
    }

    [HttpGet]
    public async Task<IActionResult> GetInvitations([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size > 100) size = 100;
        RefAsync<int> totalCount = 0;

        var invites = await _db.Queryable<InviteTable>()
            .Includes(i => i.Creator)
            .OrderByDescending(it => it.Id)
            .ToPageListAsync(page, size, totalCount);

        Response.Headers.Append("X-Total-Count", totalCount.ToString());
        return Ok(invites);
    }

    [HttpPost]
    public async Task<IActionResult> CreateInvitations([FromBody] CreateInvitationRequest request)
    {
        DateTime createdAt = DateTime.UtcNow;
        InviteTable invite = new InviteTable
        {
            MaxUses = request.Uses,
            Code = GenerateSecureCode(),
            CreatedAt = createdAt,
            ExpireAt = request.HoursValid > 0 ? createdAt.AddHours(request.HoursValid) : null,
            CreatorId = CurrentUserId,
        };

        int id = await _db.Insertable(invite).ExecuteReturnIdentityAsync();
        invite.Id = id;
        return CreatedAtAction(nameof(GetInvitation), new { id }, invite);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetInvitation(int id)
    {
        var invite = await _db.Queryable<InviteTable>().Includes(i => i.Creator).InSingleAsync(id);
        return invite == null ? NotFound() : Ok(invite);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateInvitation(int id, [FromBody] UpdateInvitationRequest request)
    {
        var invite = await _db.Queryable<InviteTable>().InSingleAsync(id);
        if (invite == null) return NotFound();

        if (request.IsDisabled.HasValue) invite.IsDisabled = request.IsDisabled.Value;
        if (request.Uses.HasValue) invite.MaxUses = request.Uses.Value;
        if (request.HoursValid.HasValue)
        {
            invite.ExpireAt = request.HoursValid.Value > 0
                ? invite.CreatedAt.AddHours(request.HoursValid.Value) : null;
        }

        await _db.Updateable(invite)
            .UpdateColumns(it => new { it.MaxUses, it.ExpireAt, it.IsDisabled }) // 补上了 IsDisabled
            .ExecuteCommandAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteInvitation(int id)
    {
        var result = await _db.Deleteable<InviteTable>().Where(i => i.Id == id).ExecuteCommandAsync();
        return result == 0 ? NotFound() : NoContent();
    }

    [HttpGet("{id:int}/users")]
    public async Task<IActionResult> GetInvitationUsers(int id)
    {
        var exists = await _db.Queryable<InviteTable>().AnyAsync(it => it.Id == id);
        if (!exists) return NotFound();

        var users = await _db.Queryable<UserTable>().Where(u => u.UsedInviteId == id).ToListAsync();
        return Ok(users);
    }
}