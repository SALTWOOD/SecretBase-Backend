using backend.SourceGenerators;

namespace backend.Services;

[GenerateSettingsTree]
public static partial class SettingRegistry
{
    public static readonly string[] Keys =
    [
        "site.seo.general.title:string",
        "site.seo.general.description:string",
        "site.seo.general.keywords:string",
        "site.seo.general.robots:string",
        "site.seo.og.title:string",
        "site.seo.og.description:string",
        "site.seo.og.image:string",
        "site.seo.social.twitter_card:string",
        "site.security.cookie.auto_renew:bool",
        "site.security.cookie.expire_hours:int",
        "site.user.registration.enabled:bool",
        "site.user.registration.force_invitation:bool",
        "site.comment.guest.enabled:bool",
        "site.comment.guest.require_approval:bool",
        "site.comment.guest.allow_reply:bool",
        "site.home.background.url:string",
        "site.home.background.blur:int",
        "site.home.background.opacity:double",
        "site.home.banner.content:string",
        "site.home.banner.display_mode:string",
        "site.footer.beian.icp:string",
        "site.footer.beian.police:string",
        "site.general.info.site_created_at:datetime",
    ];

    public static readonly Dictionary<string, object?> DefaultValues = new()
    {
        ["site.seo.general.title:string"] = "默认站点",
        ["site.seo.general.description:string"] = "基于 ASP.NET Core 与 Nuxt 4 强力驱动的站点",
        ["site.seo.general.keywords:string"] = "blog, dotnet, nuxt, site",
        ["site.seo.general.robots:string"] = "index, follow",
        ["site.seo.og.title:string"] = "Default Website",
        ["site.seo.og.description:string"] = "基于 ASP.NET Core 与 Nuxt 4 强力驱动的站点",
        ["site.seo.og.image:string"] = "/default-og-image.png",
        ["site.seo.social.twitter_card:string"] = "summary_large_image",
        ["site.security.cookie.auto_renew:bool"] = true,
        ["site.security.cookie.expire_hours:int"] = 72,
        ["site.user.registration.enabled:bool"] = true,
        ["site.user.registration.force_invitation:bool"] = false,
        ["site.comment.guest.enabled:bool"] = true,
        ["site.comment.guest.require_approval:bool"] = true,
        ["site.comment.guest.allow_reply:bool"] = true,
        ["site.home.background.url:string"] = null,
        ["site.home.background.blur:int"] = 0,
        ["site.home.background.opacity:double"] = 1.0,
        ["site.home.banner.content:string"] = "Welcome to My Site",
        ["site.home.banner.display_mode:string"] = "full",
        ["site.footer.beian.icp:string"] = "",
        ["site.footer.beian.police:string"] = "",
        ["site.general.info.site_created_at:datetime"] = new DateTime(),
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