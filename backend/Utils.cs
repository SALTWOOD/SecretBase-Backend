using backend.Tables;
using SqlSugar;
using System.Security.Cryptography;

namespace backend;

public static class Utils
{
    public static string GenerateRandomSecret(int length = 16)
    {
        char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
        string randomString = new string(RandomNumberGenerator.GetItems(chars, length));
        return randomString;
    }

    public static async Task<InviteTable?> GetInvite(ISqlSugarClient db, string? code, bool doIncrement = true)
    {
        if (string.IsNullOrEmpty(code)) return null;
        InviteTable invite = await db.Queryable<InviteTable>()
            .FirstAsync(it =>
                it.Code == code &&
                it.IsValid
            );
        if (doIncrement && invite != null)
        {
            await db.Updateable<InviteTable>()
                .Where(it => it.Id == invite.Id)
                .SetColumns(it => it.UsedCount == it.UsedCount + 1)
                .ExecuteCommandAsync();
        }
        return invite;
    }
}
