using backend.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System.Net.Mime;
using System.Security.Cryptography;

namespace backend.Controllers;

public record CreateInvitationRequest(int Uses, int HoursValid);
public record UpdateInvitationRequest(int? Uses, int? HoursValid);

[Authorize(Policy = "AdminOnly")]
[ApiController]
public class AdminController : BaseApiController
{
    public AdminController(ISqlSugarClient db, ILogger<BaseApiController> logger) : base(db, logger)
    {
    }

    private static string GenerateSecureCode()
    {
        // Generate 20 characters
        const string chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var result = RandomNumberGenerator.GetString(chars, 20);

        // Format into XXXXX-XXXXX-XXXXX-XXXXX
        return string.Join("-", Enumerable.Range(0, 4)
            .Select(i => result.Substring(i * 5, 5)));
    }

    [HttpGet("invitations")]
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
    [HttpPost("invitations")]
    [ProducesResponseType<InviteTable>(StatusCodes.Status201Created)]
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

    [HttpGet("invitations/{id:int}")]
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

    [HttpPut("invitations/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateInvitation(int id, [FromBody] UpdateInvitationRequest request)
    {
        var invite = await _db.Queryable<InviteTable>().InSingleAsync(id);
        if (invite == null)
            return NotFound(new { message = $"Invitation with ID {id} not found" });

        var changed = false;

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

    [HttpDelete("invitations/{id:int}")]
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

    /// <summary>
    /// Get all registered invites
    /// </summary>
    [HttpGet("users")]
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
    [HttpPut("users/{id:int}/status")]
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