using backend.Database;
using backend.Database.Entities;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
    [ProducesResponseType<List<Invite>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvitations([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size > 100) size = 100;

        int totalCount = 0;

        var invites = await _db.Invites
            .Include(i => i.Creator)
            .OrderByDescending(it => it.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        totalCount = invites.Count;

        Response.Headers.Append("X-Total-Count", totalCount.ToString());

        return Ok(invites);
    }

    /// <summary>
    /// Generate new invitation codes
    /// </summary>
    [HttpPost]
    [ProducesResponseType<Invite>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInvitations([FromBody] CreateInvitationRequest request)
    {
        DateTime createdAt = DateTime.UtcNow;

        Invite invite = new Invite
        {
            MaxUses = request.Uses,
            Code = Utils.GenerateSecureCode(),
            CreatedAt = createdAt,
            ExpireAt = request.HoursValid > 0 ? createdAt.AddHours(request.HoursValid) : null,
            CreatorId = CurrentUserId.ThrowIfNull(),
        };

        await _db.Invites.AddAsync(invite);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetInvitation), new { id = invite.Id }, invite);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<Invite>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvitation(int id)
    {
        var invite = await _db.Invites
            .Include(i => i.Creator)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invite == null)
            return NotFound(new { message = $"Invitation with ID {id} not found" });
        return Ok(invite);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateInvitation(int id, [FromBody] UpdateInvitationRequest request)
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

        var invite = await _db.Invites.FindAsync(id);
        if (invite == null)
            return NotFound(new { message = $"Invitation with ID {id} not found" });

        // 获取邀请创建者的角色
        var creator = await _db.Users
            .Where(u => u.Id == invite.CreatorId)
            .Select(u => new { u.Role })
            .FirstOrDefaultAsync();

        // 检查权限：只能修改等级低于自己的用户创建的邀请
        if (creator != null && creator.Role >= currentUser.Role)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Cannot modify invitations created by users with equal or higher role level" });
        }

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

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteInvitation(int id)
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

        var invite = await _db.Invites.FindAsync(id);
        if (invite == null)
            return NotFound();

        // 获取邀请创建者的角色
        var creator = await _db.Users
            .Where(u => u.Id == invite.CreatorId)
            .Select(u => new { u.Role })
            .FirstOrDefaultAsync();

        // 检查权限：只能删除等级低于自己的用户创建的邀请
        if (creator != null && creator.Role >= currentUser.Role)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Cannot delete invitations created by users with equal or higher role level" });
        }

        _db.Invites.Remove(invite);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/users")]
    [ProducesResponseType<List<User>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvitationUsers(int id, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var exists = await _db.Invites.AnyAsync(it => it.Id == id);
        if (!exists) return NotFound();

        int totalCount = 0;

        var users = await _db.Users
            .Where(u => u.UsedInviteId == id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        totalCount = users.Count;

        Response.Headers.Append("X-Total-Count", totalCount.ToString());
        return Ok(users);
    }
}
