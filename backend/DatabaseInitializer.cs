using backend.Services;
using backend.Tables;
using SqlSugar;

namespace backend;

public class DatabaseInitializer
{
    public static async Task InitializeAsync(ISqlSugarClient db, SettingService settingService)
    {
        var defaultSettings = new Dictionary<string, object>
        {
            { SettingKeys.Site.Security.Cookie.AutoRenew, true },
            { SettingKeys.Site.Security.Cookie.ExpireHours, 72 },
            { SettingKeys.Site.User.Registration.Enabled, true },
            { SettingKeys.Site.User.Registration.ForceInvitation, false }
        };

        foreach (var item in defaultSettings)
        {
            if (!await settingService.Exists(item.Key))
            {
                await settingService.Set(item.Key, item.Value);
            }
        }

        if (!await db.Queryable<UserTable>().AnyAsync())
        {
            string password = Utils.GenerateRandomSecret();
            UserTable admin = new UserTable
            {
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Admin,
            };
            await db.Insertable(admin).ExecuteCommandAsync();
            Console.WriteLine($"[DatabaseInitializer] Created default admin user. Username: 'admin', Password: '{password}'");
        }
    }
}