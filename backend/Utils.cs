using System.Security.Cryptography;

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
