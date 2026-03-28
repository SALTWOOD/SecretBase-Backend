using backend.Database;
using backend.Database.Entities;
using backend.Services;
using backend.Types.Response;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using static backend.Services.SessionService;

namespace backend.Controllers.Auth;

public readonly record struct CredentialUpdateModel(
    string? Nickname
);

[ApiController]
[Route("auth/webauthn")]
public class WebAuthnController : BaseApiController
{
    private readonly WebAuthnService _service;
    private readonly IFido2 _fido2;
    private readonly TwoFactorManager _twoFactor;
    private const string REG_PREFIX = "webauthn:reg";
    private const string LOGIN_PREFIX = "webauthn:login";

    public WebAuthnController(BaseServices deps, WebAuthnService service, IFido2 fido2, TwoFactorManager twoFactor) :
        base(deps)
    {
        _service = service;
        _fido2 = fido2;
        _twoFactor = twoFactor;
    }

    [HttpPost("register/options")]
    [Authorize]
    [ProducesResponseType<CredentialCreateOptions>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegisterOptions()
    {
        var user = await CurrentUser;
        if (user == null) return Unauthorized();
        var options = _service.GetRegistrationOptions(user.Id, user.Username);

        var cacheKey = $"{REG_PREFIX}:{Base64UrlTextEncoder.Encode(options.Challenge)}";
        _redis.StringSet(cacheKey, options.ToJson(), TimeSpan.FromMinutes(5));

        return Ok(options);
    }

    [HttpPost("register/verify")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyRegistration([FromBody] AuthenticatorAttestationRawResponse response)
    {
        using var doc = JsonDocument.Parse(response.Response.ClientDataJson);
        var challenge = doc.RootElement.GetProperty("challenge").GetString();

        var cacheKey = $"{REG_PREFIX}:{challenge}";

        var value = await _redis.StringGetAsync(cacheKey);
        if (value.IsNullOrEmpty) return BadRequest(new MessageResponse { Message = "Challenge not found" });

        var options = CredentialCreateOptions.FromJson(value!);
        await _service.VerifyAndAddCredentialAsync(response, options!, CurrentUserId.ThrowIfNull());

        await _redis.KeyDeleteAsync(cacheKey);
        return Ok();
    }

    [HttpPost("login/verify")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyLogin([FromBody] AuthenticatorAssertionRawResponse response,
        [FromQuery] bool isLogin = false)
    {
        using var doc = JsonDocument.Parse(response.Response.ClientDataJson);
        var challenge = doc.RootElement.GetProperty("challenge").GetString();

        var cacheKey = $"{LOGIN_PREFIX}:{challenge}";

        var json = await _redis.StringGetAsync(cacheKey);
        if (json.IsNullOrEmpty) return BadRequest(new MessageResponse { Message = "Challenge expired or invalid" });

        var options = AssertionOptions.FromJson(json!);

        var credential = await _service.FindCredentialByIdAsync(Base64UrlTextEncoder.Decode(response.Id));
        if (credential == null) return BadRequest(new MessageResponse { Message = "Unknown credential" });

        var result = await _fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = response,
            OriginalOptions = options,
            StoredPublicKey = credential.PublicKey,
            StoredSignatureCounter = credential.SignatureCounter,
            IsUserHandleOwnerOfCredentialIdCallback = async (args, ct) =>
                credential.UserId == BitConverter.ToInt32(args.UserHandle)
        });

        credential.SignatureCounter = result.SignCount;
        _db.UserCredentials.Update(credential);
        await _db.SaveChangesAsync();
        var user = await _db.Users.FirstAsync(it => it.Id == credential.UserId);

        var authToken = Request.Cookies[Constants.AUTH_TOKEN_COOKIE_NAME];
        if (string.IsNullOrEmpty(authToken))
            return BadRequest(new MessageResponse { Message = "Authentication token not found" });

        // Get token permission level
        var permissionLevel = await _session.GetTokenPermissionLevelAsync(authToken);

        if (!isLogin)
        {
            // If it's a no permission token, upgrade to full permission token
            if (permissionLevel == TokenPermissionLevel.None)
                await _session.UpgradeTokenAsync(authToken);
            // If it's already a full permission token, set 2FA grace period
            else if (permissionLevel == TokenPermissionLevel.Full)
                await _twoFactor.GrantGracePeriodAsync(user.Id, authToken);

            await _redis.KeyDeleteAsync(cacheKey);
            return NoContent();
        }
        else
        {
            // If it's a no permission token, upgrade to full permission token
            if (permissionLevel == TokenPermissionLevel.None)
                await _session.UpgradeTokenAsync(authToken);
            // If it's already a full permission token, set 2FA grace period
            else if (permissionLevel == TokenPermissionLevel.Full)
                await _twoFactor.GrantGracePeriodAsync(user.Id, authToken);

            var expires = await RefreshTokenAsync(user, TokenPermissionLevel.Full);
            var autoRenew = await SettingRegistry.Site.Security.Cookie.AutoRenew;
            var expiresValue = autoRenew ? DateTime.UtcNow.AddHours(expires) : (DateTime?)null;

            return Ok(new MessageResponse
            {
                Message = "Operation successful"
            });
        }
    }

    [HttpPost("login/options")]
    [ProducesResponseType<AssertionOptions>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLoginOptions()
    {
        var id = CurrentUserId;
        var credentials = id.HasValue ? await _service.GetUserCredentialsAsync(id.Value) : [];
        var existingKeys = credentials.Select(c => new PublicKeyCredentialDescriptor(c.CredentialId)).ToList();

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = existingKeys,
            UserVerification = UserVerificationRequirement.Preferred
        });

        var cacheKey = $"{LOGIN_PREFIX}:{Base64UrlTextEncoder.Encode(options.Challenge)}";
        await _redis.StringSetAsync(cacheKey, options.ToJson(), TimeSpan.FromMinutes(5));

        return Ok(options);
    }

    [HttpGet("credentials")]
    [Authorize]
    [ProducesResponseType<List<UserCredential>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCredentials()
    {
        return Ok(await _service.GetUserCredentialsAsync(CurrentUserId.ThrowIfNull()));
    }

    [HttpDelete("credentials/{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCredential(int id)
    {
        var success = await _service.DeleteCredentialAsync(id, CurrentUserId.ThrowIfNull());
        return success ? Ok() : BadRequest();
    }

    [HttpPut("credentials/{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PutCredential(int id, [FromBody] CredentialUpdateModel model)
    {
        if (model.Nickname != null)
            await _service.UpdateDeviceNicknameAsync(id, CurrentUserId.ThrowIfNull(), model.Nickname);
        return NoContent();
    }
}