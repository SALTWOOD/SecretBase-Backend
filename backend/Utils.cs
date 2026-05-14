using backend.Database;
using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace backend;

public static class Utils
{
    public static string GenerateRandomSecret(int length = 24)
    {
        var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
        var randomString = new string(RandomNumberGenerator.GetItems(chars, length));
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

        if (!doIncrement)
            return await db.Invites.FirstOrDefaultAsync(it => it.Code == code);

        // Atomic increment: only increments if within MaxUses limit
        var rowsAffected = await db.Invites
            .Where(it => it.Code == code && !it.IsDisabled && it.UsedCount < it.MaxUses)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                it => it.UsedCount, it => it.UsedCount + 1));

        if (rowsAffected == 0) return null;

        return await db.Invites.FirstOrDefaultAsync(it => it.Code == code);
    }
}