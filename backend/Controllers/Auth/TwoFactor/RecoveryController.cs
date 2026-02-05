using System.Security.Cryptography;
using System.Text;

namespace backend.Controllers.Auth.TwoFactor;

public class RecoveryController
{
    private static IEnumerable<(string, string)> GenerateRecoveryCodes(int count = 10)
    {
        for (int i = 0; i < count; i++)
        {
            string code = Utils.GenerateSecureCode();
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
            string hash = Convert.ToHexString(bytes);
            yield return (code, hash);
        }
    }
}