using backend.Services;
using backend.Tables;
using backend.Types.Response;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;

namespace backend.Controllers;

[ApiController]
[Route("auth/webauthn")]
public class WebAuthnController : BaseApiController
{
    private readonly WebAuthnService _service;
    private readonly IFido2 _fido2;
    private const string REG_PREFIX = "webauthn:reg:";
    private const string LOGIN_PREFIX = "webauthn:login:";

    public WebAuthnController(BaseServices deps, WebAuthnService service, IFido2 fido2) : base(deps)
    {
        _service = service;
        _fido2 = fido2;
    }

    [HttpPost("register/options")]
    [ProducesResponseType<CredentialCreateOptions>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegisterOptions()
    {
        var user = await CurrentUser;
        var options = _service.GetRegistrationOptions(user.Id, user.Username);

        var cacheKey = $"{REG_PREFIX}{Base64UrlTextEncoder.Encode(options.Challenge)}";
        _redis.StringSet(cacheKey, options.ToJson(), TimeSpan.FromMinutes(5));

        return Ok(options);
    }

    [HttpPost("register/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyRegistration([FromBody] AuthenticatorAttestationRawResponse response)
    {
        var clientDataJson = Encoding.UTF8.GetString(response.Response.ClientDataJson);
        var clientData = JObject.Parse(clientDataJson);
        var challenge = clientData["challenge"]?.ToString();

        var cacheKey = $"{REG_PREFIX}{challenge}";

        var value = await _redis.StringGetAsync(cacheKey);
        if (value.IsNullOrEmpty) return BadRequest(new MessageResponse { Message = "Challenge not found" });

        var options = CredentialCreateOptions.FromJson(value!);
        await _service.VerifyAndAddCredentialAsync(response, options!, CurrentUserId);

        await _redis.KeyDeleteAsync(cacheKey);
        return Ok();
    }

    [HttpPost("login/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyLogin([FromBody] AuthenticatorAssertionRawResponse response)
    {
        var clientDataJson = Encoding.UTF8.GetString(response.Response.ClientDataJson);
        var clientData = JObject.Parse(clientDataJson);
        var challenge = clientData["challenge"]?.ToString();

        var cacheKey = $"{LOGIN_PREFIX}{challenge}";

        var json = await _redis.StringGetAsync(cacheKey);
        if (json.IsNullOrEmpty) return BadRequest(new MessageResponse { Message = "Challenge expired or invalid" });
        Console.WriteLine("Cached Options JSON: " + json);

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
        await _db.Updateable(credential).ExecuteCommandAsync();

        var token = await RefreshTokenAsync(await _db.Queryable<UserTable>().FirstAsync(it => it.Id == credential.UserId));

        await _redis.KeyDeleteAsync(cacheKey);
        return Ok(new { token });
    }

    [HttpPost("login/options")]
    [ProducesResponseType<AssertionOptions>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLoginOptions()
    {
        var credentials = await _service.GetUserCredentialsAsync(CurrentUserId);
        var existingKeys = credentials.Select(c => new PublicKeyCredentialDescriptor(c.CredentialId)).ToList();

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = existingKeys,
            UserVerification = UserVerificationRequirement.Preferred
        });

        var cacheKey = $"{LOGIN_PREFIX}{Base64UrlTextEncoder.Encode(options.Challenge)}";
        await _redis.StringSetAsync(cacheKey, options.ToJson(), TimeSpan.FromMinutes(5));

        return Ok(options);
    }

    [HttpGet("credentials")]
    [ProducesResponseType<List<UserCredentialTable>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCredentials()
        => Ok(await _service.GetUserCredentialsAsync(CurrentUserId));

    [HttpDelete("credentials/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCredential(int id)
    {
        var success = await _service.DeleteCredentialAsync(id, CurrentUserId);
        return success ? Ok() : BadRequest();
    }
}