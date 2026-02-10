using backend.Database;
using backend.Database.Entities;
using backend.Filters;
using backend.Services;
using backend.Types.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using System.Security.Cryptography;
using System.Text;
using static backend.Services.SessionService;

namespace backend.Controllers.Auth.TwoFactor;

public readonly record struct TotpVerifyRequest(string Code);
public readonly record struct TotpRecoveryCodeRequest(string Code);
public readonly record struct TotpSetupResponse(string Secret, string Url);

[ApiController]
[Route("auth/two-factor/totp")]
public class TotpController : BaseApiController
{
    private readonly TwoFactorManager _twoFactor;
    private const string ISSUER = "SecretBase";
    private const string PREFIX = "totp_setup";

    public TotpController(BaseServices deps, TwoFactorManager twoFactor) : base(deps)
    {
        _twoFactor = twoFactor;
    }


    [HttpPost("setup")]
    [ProducesResponseType<TotpSetupResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Setup()
    {
        var user = await CurrentUser;

        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secretBase32 = Base32Encoding.ToString(secretBytes);

        string account = user.Username;
        var label = $"{Uri.EscapeDataString(ISSUER)}:{Uri.EscapeDataString(account)}";

        var baseUri = $"otpauth://totp/{label}";

        var queryParams = new Dictionary<string, string?>
        {
            { "secret", secretBase32 },
            { "issuer", ISSUER },
            { "digits", "6" },
            { "period", "30" }
        };

        var qrCodeUrl = QueryHelpers.AddQueryString(baseUri, queryParams);

        await _redis.StringSetAsync($"{PREFIX}:{user.Id}", secretBase32, TimeSpan.FromMinutes(10));

        return Ok(new TotpSetupResponse
        {
            Secret = secretBase32,
            Url = qrCodeUrl
        });
    }

    [HttpPost("enable")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enable([FromBody] TotpVerifyRequest request)
    {
        var user = await CurrentUser;
        var tempSecret = await _redis.StringGetAsync($"{PREFIX}:{user.Id}");
        if (tempSecret.IsNullOrEmpty) return BadRequest(new MessageResponse { Message = "Setup session expired" });

        var totp = new Totp(Base32Encoding.ToBytes(tempSecret!));
        if (totp.VerifyTotp(request.Code, out _, VerificationWindow.RfcSpecifiedNetworkDelay))
        {
            user.TotpSecret = tempSecret;

            await _db.SaveChangesAsync();

            await _redis.KeyDeleteAsync($"{PREFIX}:{user.Id}");

            return Ok(new MessageResponse { Message = "Successfully set up TOTP" });
        }
        return BadRequest(new MessageResponse { Message = "Invalid code" });
    }

    [HttpPost("disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable()
    {
        var user = await CurrentUser;
        if (user.TotpSecret == null) return BadRequest(new MessageResponse { Message = "TOTP has not been set up yet" });
        user.TotpSecret = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("verify")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Verify([FromBody] TotpVerifyRequest request)
    {
        User user = await CurrentUser;
        string? savedSecret = user.TotpSecret;

        if (string.IsNullOrEmpty(savedSecret)) return BadRequest(new MessageResponse { Message = "TOTP has not been set up" });

        var secretBytes = Base32Encoding.ToBytes(savedSecret);
        var totp = new Totp(secretBytes);
        bool isValid = totp.VerifyTotp(request.Code, out _, VerificationWindow.RfcSpecifiedNetworkDelay);

        if (!isValid) return BadRequest(new MessageResponse { Message = "Invalid verification code" });

        var authToken = Request.Cookies[Constants.AUTH_TOKEN_COOKIE_NAME];
        if (string.IsNullOrEmpty(authToken))
        {
            return BadRequest(new MessageResponse { Message = "Authentication token not found" });
        }

        // 获取 token 权限级别
        var permissionLevel = await _session.GetTokenPermissionLevelAsync(authToken);

        // 如果是无权限 token，升级为完全权限 token
        if (permissionLevel == TokenPermissionLevel.None)
        {
            await _session.UpgradeTokenAsync(authToken);
        }
        // 如果已经是完全权限 token，设置 2FA 宽限期
        else if (permissionLevel == TokenPermissionLevel.Full)
        {
            await _twoFactor.GrantGracePeriodAsync(user.Id, authToken);
        }

        return Ok(new { message = "TOTP verified" });
    }
}
