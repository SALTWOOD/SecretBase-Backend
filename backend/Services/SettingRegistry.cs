using backend.SourceGenerators;

namespace backend.Services;

[GenerateSettingsTree]
public static partial class SettingRegistry
{
    public static Dictionary<string, object?>.KeyCollection Keys => DefaultValues.Keys;

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
        ["site.user.github.client_id:string"] = "",
        ["site.user.github.client_secret:string"] = "",
        ["site.comment.guest.enabled:bool"] = true,
        ["site.comment.guest.require_approval:bool"] = true,
        ["site.comment.guest.allow_reply:bool"] = true,
        ["site.home.background.url:string"] = null,
        ["site.home.background.blur:int"] = 0,
        ["site.home.background.brightness:int"] = 100,
        ["site.home.banner.content:string"] = "Welcome to My Site",
        ["site.home.banner.display_mode:string"] = "full",
        ["site.home.sidebar.left:json"] = null,
        ["site.home.sidebar.right:json"] = null,
        ["site.home.header.icon:string"] = "i-lucide-zap",
        ["site.home.header.icon_type:string"] = "icon",
        ["site.home.header.title:string"] = "Secret Base",
        ["site.home.header.links:json"] = "[]",
        ["site.home.header.show_color_mode:bool"] = true,
        ["site.home.header.show_github:bool"] = true,
        ["site.home.header.github_url:string"] = "https://github.com",
        ["site.footer.beian.icp:string"] = "",
        ["site.footer.beian.police:string"] = "",
        ["site.general.info.site_created_at:datetime"] = new DateTime(),
        ["site.live.enabled:bool"] = false,
        ["site.live.admin_only:bool"] = true,
        ["site.live.hook_secret:string"] = "",
        ["site.live.rtmp_server:string"] = "rtmp://localhost/live",
        ["site.live.playback_base_url:string"] = "/hls",
        ["site.live.danmaku_enabled:bool"] = true,
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
