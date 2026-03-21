using System.Security.Cryptography;
using backend.Database.Models;

namespace backend;

public static class Utils
{
    public static string GenerateRandomSecret(int length = 24)
    {
        char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
        string randomString = new string(RandomNumberGenerator.GetItems(chars, length));
        return randomString;
    }

    public static string GenerateSecureCode()
    {
        const string chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var result = RandomNumberGenerator.GetString(chars, 20);
        return string.Join("-", Enumerable.Range(0, 4).Select(i => result.Substring(i * 5, 5)));
    }

    public static async Task<Invite?> GetInvite(Supabase.Client client, string? code, bool doIncrement = true)
    {
        if (string.IsNullOrEmpty(code)) return null;
        Invite? invite = await client.From<Invite>()
            .Where(i => i.Code == code)
            .Single();
        
        if (doIncrement && invite != null && invite.IsValid)
        {
            invite.UsedCount++;
            await client.From<Invite>().Update(invite);
        }
        return invite;
    }
}
