using backend.Database;
using backend.Database.Entities;
using backend.Services;
using backend.SourceGenerators;
using Microsoft.EntityFrameworkCore;

namespace backend;

public class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        foreach (var (keyWithType, defaultValue) in SettingRegistry.DefaultValues)
        {
            var key = SettingRegistry.ExtractKey(keyWithType);
            if (!await db.Settings.AnyAsync(s => s.Key == key))
            {
                var setting = new Setting { Key = key };
                setting.SetValue(defaultValue);
                db.Settings.Add(setting);
            }
        }
        await db.SaveChangesAsync();

        if (!await db.Users.AnyAsync())
        {
            var password = Utils.GenerateRandomSecret();
            var admin = new User
            {
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Owner
            };
            await db.Users.AddAsync(admin);
            await db.SaveChangesAsync();
            Console.WriteLine(
                $"[DatabaseInitializer] Created default admin user. Username: 'admin', Password: '{password}'");
        }
    }
}