using backend.Database;
using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

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

    public static async Task<Invite?> GetInvite(AppDbContext db, string? code, bool doIncrement = true)
    {
        if (string.IsNullOrEmpty(code)) return null;
        Invite? invite = await db.Invites
            .AsNoTracking()
            .FirstOrDefaultAsync(it => it.Code == code);
        
        if (doIncrement && invite != null && invite.IsValid)
        {
            var dbInvite = await db.Invites.FindAsync(invite.Id);
            if (dbInvite != null)
            {
                dbInvite.UsedCount++;
                await db.SaveChangesAsync();
            }
        }
        return invite;
    }
}
