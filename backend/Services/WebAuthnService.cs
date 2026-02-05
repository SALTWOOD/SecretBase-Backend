using backend.Tables;
using Fido2NetLib;
using Fido2NetLib.Objects;
using SqlSugar;

namespace backend.Services;

public class WebAuthnService
{
    private readonly IFido2 _fido2;
    private readonly ISqlSugarClient _db;

    public WebAuthnService(IFido2 fido2, ISqlSugarClient db)
    {
        _fido2 = fido2;
        _db = db;
    }

    public CredentialCreateOptions GetRegistrationOptions(int userId, string username)
    {
        var user = new Fido2User
        {
            DisplayName = username,
            Name = username,
            Id = BitConverter.GetBytes(userId)
        };

        var existingKeys = _db.Queryable<UserCredentialTable>()
            .Where(c => c.UserId == userId)
            .ToList()
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        var authenticatorSelection = new AuthenticatorSelection
        {
            ResidentKey = ResidentKeyRequirement.Required,
            UserVerification = UserVerificationRequirement.Preferred
        };

        return _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = user,
            AuthenticatorSelection = authenticatorSelection,
            ExcludeCredentials = existingKeys,
            AttestationPreference = AttestationConveyancePreference.None
        });
    }

    public async Task<UserCredentialTable> VerifyAndAddCredentialAsync(
        AuthenticatorAttestationRawResponse attestationResponse,
        CredentialCreateOptions origOptions,
        int userId)
    {
        var res = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestationResponse,
            OriginalOptions = origOptions,
            IsCredentialIdUniqueToUserCallback = async (args, ct) =>
            {
                return !await _db.Queryable<UserCredentialTable>()
                    .AnyAsync(c => c.CredentialId == args.CredentialId && c.UserId == userId);
            }
        });

        var credential = new UserCredentialTable
        {
            UserId = userId,
            CredentialId = res.Id,
            PublicKey = res.PublicKey,
            SignatureCounter = res.SignCount,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Insertable(credential).ExecuteCommandAsync();
        return credential;
    }

    public async Task<UserCredentialTable?> FindCredentialByIdAsync(byte[] credentialId)
    {
        return await _db.Queryable<UserCredentialTable>()
            .FirstAsync(c => c.CredentialId == credentialId);
    }

    public async Task<List<UserCredentialTable>> GetUserCredentialsAsync(int userId)
    {
        return await _db.Queryable<UserCredentialTable>()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.Id)
            .ToListAsync();
    }

    public async Task<bool> DeleteCredentialAsync(int id, int userId)
    {
        return await _db.Deleteable<UserCredentialTable>()
            .Where(c => c.Id == id && c.UserId == userId)
            .ExecuteCommandHasChangeAsync();
    }

    public async Task<bool> UpdateDeviceNicknameAsync(int id, int userId, string nickname)
    {
        return await _db.Updateable<UserCredentialTable>()
            .SetColumns(c => c.Nickname == nickname)
            .Where(c => c.Id == id && c.UserId == userId)
            .ExecuteCommandHasChangeAsync();
    }
}