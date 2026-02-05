using backend.Services;
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
public class InvitationAdminController(BaseServices deps) : BaseApiController(deps)
{
    [HttpGet]
    [ProducesResponseType<List<InviteTable>>(StatusCodes.Status200OK)]
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

    /// <summary>
    /// Generate new invitation codes
    /// </summary>
    [HttpPost]
    [ProducesResponseType<InviteTable>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInvitations([FromBody] CreateInvitationRequest request)
    {
        DateTime createdAt = DateTime.UtcNow;

        InviteTable invite = new InviteTable
        {
            MaxUses = request.Uses,
            Code = Utils.GenerateSecureCode(),
            CreatedAt = createdAt,
            ExpireAt = request.HoursValid > 0 ? createdAt.AddHours(request.HoursValid) : null,
            CreatorId = CurrentUserId,
        };

        int id = await _db.Insertable(invite).ExecuteReturnIdentityAsync();
        invite.Id = id;

        return CreatedAtAction(nameof(GetInvitation), new { id }, invite);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<InviteTable>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvitation(int id)
    {
        var invite = await _db.Queryable<InviteTable>()
            .Includes(i => i.Creator)
            .InSingleAsync(id);
        if (invite == null)
            return NotFound(new { message = $"Invitation with ID {id} not found" });
        return Ok(invite);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateInvitation(int id, [FromBody] UpdateInvitationRequest request)
    {
        var invite = await _db.Queryable<InviteTable>().InSingleAsync(id);
        if (invite == null)
            return NotFound(new { message = $"Invitation with ID {id} not found" });

        var changed = false;

        if (request.IsDisabled.HasValue)
        {
            invite.IsDisabled = request.IsDisabled.Value;
            changed = true;
        }

        if (request.Uses.HasValue)
        {
            invite.MaxUses = request.Uses.Value;
            changed = true;
        }

        if (request.HoursValid.HasValue)
        {
            invite.ExpireAt = request.HoursValid.Value > 0
                ? invite.CreatedAt.AddHours(request.HoursValid.Value)
                : null;
            changed = true;
        }

        if (!changed)
            return NoContent();

        var result = await _db.Updateable(invite)
            .UpdateColumns(it => new { it.MaxUses, it.ExpireAt })
            .ExecuteCommandAsync();

        if (result == 0)
            return NotFound(new { message = $"Invitation with ID {id} not found" });

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteInvitation(int id)
    {
        var result = await _db.Deleteable<InviteTable>()
            .Where(i => i.Id == id)
            .ExecuteCommandAsync();
        if (result == 0)
            return NotFound();
        return NoContent();
    }

    [HttpGet("{id:int}/users")]
    [ProducesResponseType<List<UserTable>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvitationUsers(int id, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var exists = await _db.Queryable<InviteTable>().AnyAsync(it => it.Id == id);
        if (!exists) return NotFound();

        RefAsync<int> totalCount = 0;

        var users = await _db.Queryable<UserTable>()
            .Where(u => u.UsedInviteId == id)
            .ToPageListAsync(page, size, totalCount);

        Response.Headers.Append("X-Total-Count", totalCount.ToString());
        return Ok(users);
    }
}