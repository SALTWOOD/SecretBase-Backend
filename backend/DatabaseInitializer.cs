using backend.Tables;
using SqlSugar;
using System.Security.Cryptography;

namespace backend;

public class DatabaseInitializer
{
    public static async Task InitializeAsync(ISqlSugarClient db)
    {
        var defaultSettings = new Dictionary<string, object>
        {
            {
                SettingKeys.Site.Security.Jwt.Secret,
                GenerateRandomSecret()
            },
            {
                SettingKeys.Site.Security.Jwt.Issuer,
                "your-api-domain.com"
            },
            {
                SettingKeys.Site.Security.Jwt.Audience,
                "your-nuxt-app.com"
            },
            {
                SettingKeys.Site.Security.Jwt.ExpireHours,
                72
            },
            {
                SettingKeys.Site.User.Registration.Enabled,
                true
            },
            {
                SettingKeys.Site.User.Registration.ForceInvitation,
                false
            }
        };

        var existingKeys = await db.Queryable<SettingTable>()
            .Select(s => s.Key)
            .ToListAsync();

        var toInsert = new List<SettingTable>();

        foreach (var item in defaultSettings)
        {
            if (!existingKeys.Contains(item.Key))
            {
                SettingTable setting = new SettingTable
                {
                    Key = item.Key
                };
                setting.SetValue(item.Value);
                toInsert.Add(setting);
            }
        }

        if (toInsert.Any())
        {
            await db.Insertable(toInsert).ExecuteCommandAsync();
        }

        // Check for users
        if (db.Queryable<UserTable>().Count() == 0)
        {
            string password = Utils.GenerateRandomSecret();
            UserTable admin = new UserTable
            {
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Admin,
            };
            db.Insertable(admin).ExecuteCommand();
            Console.WriteLine($"[DatabaseInitializer] Created default admin user. Username: 'admin', Password: '{password}'");
        }
    }

    private static string GenerateRandomSecret()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}