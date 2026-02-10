using backend.Database;
using backend.Database.Entities;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class WebAuthnService
{
    private readonly IFido2 _fido2;
    private readonly AppDbContext _db;

    public WebAuthnService(IFido2 fido2, AppDbContext db)
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

        var existingKeys = _db.UserCredentials
            .AsNoTracking()
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

    public async Task<UserCredential> VerifyAndAddCredentialAsync(
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
                return !await _db.UserCredentials
                    .AnyAsync(c => c.CredentialId == args.CredentialId && c.UserId == userId);
            }
        });

        var credential = new UserCredential
        {
            UserId = userId,
            CredentialId = res.Id,
            PublicKey = res.PublicKey,
            SignatureCounter = res.SignCount,
            CreatedAt = DateTime.UtcNow
        };

        await _db.UserCredentials.AddAsync(credential);
        await _db.SaveChangesAsync();
        return credential;
    }

    public async Task<UserCredential?> FindCredentialByIdAsync(byte[] credentialId)
    {
        return await _db.UserCredentials
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId);
    }

    public async Task<List<UserCredential>> GetUserCredentialsAsync(int userId)
    {
        return await _db.UserCredentials
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.Id)
            .ToListAsync();
    }

    public async Task<bool> DeleteCredentialAsync(int id, int userId)
    {
        var credential = await _db.UserCredentials
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        
        if (credential == null)
            return false;

        _db.UserCredentials.Remove(credential);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateDeviceNicknameAsync(int id, int userId, string nickname)
    {
        var credential = await _db.UserCredentials
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        
        if (credential == null)
            return false;

        credential.Nickname = nickname;
        await _db.SaveChangesAsync();
        return true;
    }
}