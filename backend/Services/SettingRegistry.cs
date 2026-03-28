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
        "site.home.background.opacity:int", // 透明度

        // --- Home Banner ---
        "site.home.banner.content:string",
        "site.home.banner.display_mode:string" // full | mini | screen | hidden
    ];
}