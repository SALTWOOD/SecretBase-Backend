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
        var defaultSettings = new Dictionary<SettingNode, object?>
        {
            // Cookie
            { SettingRegistry.Site.Security.Cookie.AutoRenew, true },
            { SettingRegistry.Site.Security.Cookie.ExpireHours, 72 },

            // Registration
            { SettingRegistry.Site.User.Registration.Enabled, true },
            { SettingRegistry.Site.User.Registration.ForceInvitation, false },

            // SEO Default Values
            { SettingRegistry.Site.Seo.Title, "默认站点" },
            { SettingRegistry.Site.Seo.Description, "基于 ASP.NET Core 与 Nuxt 4 强力驱动的站点" },
            { SettingRegistry.Site.Seo.Keywords, "blog, dotnet, nuxt, site" },
            { SettingRegistry.Site.Seo.OgTitle, "Default Website" },
            { SettingRegistry.Site.Seo.OgDescription, "基于 ASP.NET Core 与 Nuxt 4 强力驱动的站点" },
            { SettingRegistry.Site.Seo.OgImage, "/default-og-image.png" },
            { SettingRegistry.Site.Seo.TwitterCard, "summary_large_image" },
            { SettingRegistry.Site.Seo.Robots, "index, follow" },

            // Background Settings
            { SettingRegistry.Site.Home.Background.Url, null },
            { SettingRegistry.Site.Home.Background.Blur, 0 },      // 默认不虚化
            { SettingRegistry.Site.Home.Background.Opacity, 1.0 }, // 默认不透明

            // Banner Settings
            { SettingRegistry.Site.Home.Banner.Content, "Welcome to My Site" },
            { SettingRegistry.Site.Home.Banner.DisplayMode, "full" } // 默认为全屏高度
        };

        foreach (var (key, value) in defaultSettings)
        {
            if (!await key.ExistsAsync())
            {
                await key.SetValueAsync(value);
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