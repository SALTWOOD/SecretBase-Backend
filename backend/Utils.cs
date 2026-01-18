using backend.Services;
using backend.Tables;
using SqlSugar;
using System.Security.Cryptography;
using static backend.Tables.SettingKeys.Site.Security;

namespace backend
{
    public static class Utils
    {
        public static string GenerateRandomSecret(int length = 16)
        {
            char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
            string randomString = new string(RandomNumberGenerator.GetItems(chars, length));
            return randomString;
        }
    }
}
