using backend.Database;
using backend.Database.Entities;
using backend.Filters;
using backend.Services;
using backend.Types.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using System.Security.Cryptography;
using System.Text;

namespace backend.Controllers.Auth.TwoFactor;

public readonly record struct TwoFactorPolicyResponse(
    TwoFactorStatus Totp,
    bool ForceTwoFactor
);
public readonly record struct TwoFactorStatus(bool Enabled);

[ApiController]
[Route("auth/two-factor/policy")]
public class TwoFactorPolicyController : BaseApiController
{
    public TwoFactorPolicyController(BaseServices deps) : base(deps) { }

    [HttpGet]
    [ProducesResponseType<TwoFactorPolicyResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolicy()
    {
        var user = await CurrentUser;
        return Ok(new TwoFactorPolicyResponse
        {
            Totp = new TwoFactorStatus(!string.IsNullOrEmpty(user.TotpSecret)),
            ForceTwoFactor = user.ForceTwoFactor
        });
    }

    [HttpPost("enable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnableForce2Fa()
    {
        var user = await CurrentUser;

        bool is2faReady = !string.IsNullOrEmpty(user.TotpSecret);
        if (!is2faReady) is2faReady = await _db.UserCredentials.AnyAsync(it => it.UserId == user.Id);

        if (!is2faReady) return BadRequest(new MessageResponse { Message = "Please set up at least one 2FA method..." });

        if (user.ForceTwoFactor) return BadRequest(new MessageResponse { Message = "2FA is already enforced for this account" });

        var codesWithHashes = GenerateRecoveryCodes(10).ToList();
        var rawCodes = codesWithHashes.Select(x => x.Code).ToArray();
        var hashedCodes = codesWithHashes.Select(x => x.Hash).ToArray();

        user.ForceTwoFactor = true;
        user.TotpRecoveryCodes = hashedCodes;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Two-factor authentication is now enforced",
            recoveryCodes = rawCodes
        });
    }

    [Require2FA]
    [HttpPost("disable")]
    public async Task<IActionResult> DisableForce2Fa()
    {
        var user = await CurrentUser;

        if (!user.ForceTwoFactor)
        {
            return BadRequest(new MessageResponse { Message = "2FA enforcement is not enabled" });
        }

        user.ForceTwoFactor = false;
        user.TotpRecoveryCodes = null;

        await _db.SaveChangesAsync();

        return Ok(new MessageResponse { Message = "Two-factor enforcement has been disabled" });
    }

    #region Helpers

    private static IEnumerable<(string Code, string Hash)> GenerateRecoveryCodes(int count)
    {
        for (int i = 0; i < count; i++)
        {
            string code = RandomNumberGenerator.GetInt32(1000_0000, 1_0000_0000).ToString();
            yield return (code, HashCode(code));
        }
    }

    private static string HashCode(string code)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    #endregion
}