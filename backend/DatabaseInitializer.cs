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
            { "site.user.registration.force_invitation", false },

            // SEO Default Values
            { SettingKeys.Site.Seo.Title, "默认站点" },
            { SettingKeys.Site.Seo.Description, "基于 ASP.NET Core 与 Nuxt 4 强力驱动的站点" },
            { SettingKeys.Site.Seo.Keywords, "blog, dotnet, nuxt, site" },
            { SettingKeys.Site.Seo.OgTitle, "Default Website" },
            { SettingKeys.Site.Seo.OgDescription, "基于 ASP.NET Core 与 Nuxt 4 强力驱动的站点" },
            { SettingKeys.Site.Seo.OgImage, "/default-og-image.png" },
            { SettingKeys.Site.Seo.TwitterCard, "summary_large_image" },
            { SettingKeys.Site.Seo.Robots, "index, follow" }
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