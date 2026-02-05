using backend.Services;
using backend.Tables;
using backend.Types.Response;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;

namespace backend.Controllers;

[ApiController]
[Route("auth/webauthn")]
public class WebAuthnController : BaseApiController
{
    const string PREFIX = "webauthn:login:";

    private readonly IFido2 _fido2;

    public WebAuthnController(BaseServices deps, IFido2 fido2) : base(deps) => _fido2 = fido2;

    [HttpPost("options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLoginOptions()
    {
        // Fetch existing credentials from PostgreSQL
        var existingCredentials = _db.Queryable<UserCredential>()
            .Where(c => c.UserId == CurrentUserId)
            .Select(c => new PublicKeyCredentialDescriptor(Base64Url.DecodeFromChars(c.Id))).ToList();

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = existingCredentials,
            UserVerification = UserVerificationRequirement.Preferred
        });

        // Store challenge in cache/session for verification
        await _redis.StringSetAsync($"{PREFIX}{Base64Url.EncodeToString(options.Challenge)}", JsonConvert.SerializeObject(options));

        return Ok(options);
    }

    [HttpPost("verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyLogin([FromBody] AuthenticatorAssertionRawResponse response)
    {
        var options = await _redis.StringGetAsync(response.Id);
        if (options.IsNull) return BadRequest(new MessageResponse { Message = "Option is null" });

        // Retrieve stored public key from DB
        var credential = await _db.Queryable<UserCredential>().FirstAsync(c => c.Id == response.Id);

        var result = await _fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = response,
            StoredSignatureCounter = credential.SignatureCounter,
            StoredPublicKey = credential.PublicKey,
            OriginalOptions = JsonConvert.DeserializeObject<AssertionOptions>(options!).ThrowIfNull(),
            IsUserHandleOwnerOfCredentialIdCallback = async (args, ct) => true
        });

        if (result == null)
            return BadRequest("Assertion verification failed.");

        // Update counter in DB to prevent replay attacks
        credential.SignatureCounter = result.SignCount;
        await _db.Updateable(credential).ExecuteCommandAsync();

        return Ok(new { token = "your_jwt_here" });
    }
}