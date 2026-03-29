using backend.SourceGenerators;

namespace backend.Services;

[GenerateSettingsTree]
public static partial class SettingRegistry
{
    public static readonly string[] Keys =
    [
        // --- Site SEO ---
        "site.seo.title:string",
        "site.seo.description:string",
        "site.seo.keywords:string",
        "site.seo.og_title:string",
        "site.seo.og_description:string",
        "site.seo.og_image:string",
        "site.seo.twitter_card:string",
        "site.seo.robots:string",

        // --- Site Security ---
        "site.security.cookie.auto_renew:bool",
        "site.security.cookie.expire_hours:int",

        // --- User Registration ---
        "site.user.registration.enabled:bool",
        "site.user.registration.force_invitation:bool",

        // --- Home Background ---
        "site.home.background.url:string",
        "site.home.background.blur:int", // 虚化度
        "site.home.background.opacity:double", // 透明度

        // --- Home Banner ---
        "site.home.banner.content:string",
        "site.home.banner.display_mode:string" // full | mini | screen | hidden
    ];

    public static readonly Dictionary<string, object?> DefaultValues = new()
    {
        // --- Site SEO ---
        ["site.seo.title:string"] = "默认站点",
        ["site.seo.description:string"] = "基于 ASP.NET Core 与 Nuxt 4 强力驱动的站点",
        ["site.seo.keywords:string"] = "blog, dotnet, nuxt, site",
        ["site.seo.og_title:string"] = "Default Website",
        ["site.seo.og_description:string"] = "基于 ASP.NET Core 与 Nuxt 4 强力驱动的站点",
        ["site.seo.og_image:string"] = "/default-og-image.png",
        ["site.seo.twitter_card:string"] = "summary_large_image",
        ["site.seo.robots:string"] = "index, follow",

        // --- Site Security ---
        ["site.security.cookie.auto_renew:bool"] = true,
        ["site.security.cookie.expire_hours:int"] = 72,

        // --- User Registration ---
        ["site.user.registration.enabled:bool"] = true,
        ["site.user.registration.force_invitation:bool"] = false,

        // --- Home Background ---
        ["site.home.background.url:string"] = null,
        ["site.home.background.blur:int"] = 0,
        ["site.home.background.opacity:double"] = 1.0,

        // --- Home Banner ---
        ["site.home.banner.content:string"] = "Welcome to My Site",
        ["site.home.banner.display_mode:string"] = "full"
    };

    /// <summary>
    /// 从 key:type 格式中提取 key
    /// </summary>
    public static string ExtractKey(string keyWithType)
    {
        var idx = keyWithType.IndexOf(':');
        return idx > 0 ? keyWithType.Substring(0, idx) : keyWithType;
    }

    /// <summary>
    /// 从 key:type 格式中提取类型
    /// </summary>
    public static string ExtractType(string keyWithType)
    {
        var idx = keyWithType.IndexOf(':');
        return idx > 0 ? keyWithType.Substring(idx + 1) : "string";
    }
}