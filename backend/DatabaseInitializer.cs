using backend.Database;
using backend.Database.Entities;
using backend.Services;
using Microsoft.EntityFrameworkCore;

namespace backend;

public class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, SettingService settingService)
    {
        var defaultSettings = new Dictionary<string, object>
        {
            { "site.security.cookie.auto_renew", true },
            { "site.security.cookie.expire_hours", 72 },
            { "site.user.registration.enabled", true },
            { "site.user.registration.force_invitation", false }
        };

        foreach (var item in defaultSettings)
        {
            if (!await settingService.Exists(item.Key))
            {
                await settingService.Set(item.Key, item.Value);
            }
        }

        if (!await db.Users.AnyAsync())
        {
            string password = Utils.GenerateRandomSecret();
            User admin = new User
            {
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Owner,
            };
            await db.Users.AddAsync(admin);
            await db.SaveChangesAsync();
            Console.WriteLine($"[DatabaseInitializer] Created default admin user. Username: 'admin', Password: '{password}'");
        }
    }
}