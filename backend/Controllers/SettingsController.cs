using backend.Database.Entities;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Frozen;

namespace backend.Controllers;

// --- Response DTOs ---

public readonly record struct HomeAppearanceResponse(
    string BackgroundUrl,
    int BackgroundBlur,
    double BackgroundOpacity,
    string BannerContent,
    string BannerDisplayMode
);

public readonly record struct SeoMetaResponse(
    string Title,
    string Description,
    string? Keywords = null,
    string? OgTitle = null,
    string? OgDescription = null,
    string? OgImage = null,
    string TwitterCard = "summary_large_image",
    string Robots = "index, follow"
);

public readonly record struct SiteInitResponse(
    SeoMetaResponse Seo,
    HomeAppearanceResponse Home,
    bool RegistrationEnabled
);

// --- Controller ---

[Route("settings")]
public class SettingsController(BaseServices deps, SettingService settings) : BaseApiController(deps)
{
    // 定义不同场景需要的 Key 集合
    private static readonly FrozenSet<string> SeoKeys = new[] {
        SettingKeys.Site.Seo.Title,
        SettingKeys.Site.Seo.Description,
        SettingKeys.Site.Seo.Keywords,
        SettingKeys.Site.Seo.OgTitle,
        SettingKeys.Site.Seo.OgDescription,
        SettingKeys.Site.Seo.OgImage,
        SettingKeys.Site.Seo.TwitterCard,
        SettingKeys.Site.Seo.Robots
    }.ToFrozenSet();

    private static readonly FrozenSet<string> HomeKeys = new[] {
        SettingKeys.Site.Home.Background.Url,
        SettingKeys.Site.Home.Background.Blur,
        SettingKeys.Site.Home.Background.Opacity,
        SettingKeys.Site.Home.Banner.Content,
        SettingKeys.Site.Home.Banner.DisplayMode
    }.ToFrozenSet();

    public static readonly FrozenSet<string> AllowedKeys = SeoKeys
        .Union(HomeKeys)
        .Append(SettingKeys.Site.User.Registration.Enabled)
        .ToFrozenSet();

    [HttpGet("seo")]
    [ProducesResponseType(typeof(SeoMetaResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SeoMetaResponse>> GetSeo()
    {
        var values = await settings.GetBatch(SeoKeys);
        return Ok(MapSeo(values));
    }

    [HttpGet("home")]
    [ProducesResponseType(typeof(HomeAppearanceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeAppearanceResponse>> GetHomeAppearance()
    {
        var values = await settings.GetBatch(HomeKeys);
        return Ok(MapHome(values));
    }

    [HttpGet("init")]
    [ProducesResponseType(typeof(SiteInitResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SiteInitResponse>> GetSiteInit()
    {
        var all = await settings.GetBatch(AllowedKeys);

        return Ok(new SiteInitResponse(
            Seo: MapSeo(all),
            Home: MapHome(all),
            RegistrationEnabled: Convert.ToBoolean(all.GetValueOrDefault(SettingKeys.Site.User.Registration.Enabled) ?? true)
        ));
    }

    private static SeoMetaResponse MapSeo(IReadOnlyDictionary<string, object?> v) => new(
        Title: v.GetValueOrDefault(SettingKeys.Site.Seo.Title) as string ?? "Default Title",
        Description: v.GetValueOrDefault(SettingKeys.Site.Seo.Description) as string ?? "",
        Keywords: v.GetValueOrDefault(SettingKeys.Site.Seo.Keywords) as string,
        OgTitle: v.GetValueOrDefault(SettingKeys.Site.Seo.OgTitle) as string,
        OgDescription: v.GetValueOrDefault(SettingKeys.Site.Seo.OgDescription) as string,
        OgImage: v.GetValueOrDefault(SettingKeys.Site.Seo.OgImage) as string,
        TwitterCard: v.GetValueOrDefault(SettingKeys.Site.Seo.TwitterCard) as string ?? "summary_large_image",
        Robots: v.GetValueOrDefault(SettingKeys.Site.Seo.Robots) as string ?? "index, follow"
    );

    private static HomeAppearanceResponse MapHome(IReadOnlyDictionary<string, object?> v) => new(
        BackgroundUrl: v.GetValueOrDefault(SettingKeys.Site.Home.Background.Url) as string ?? "",
        BackgroundBlur: Convert.ToInt32(v.GetValueOrDefault(SettingKeys.Site.Home.Background.Blur) ?? 0),
        BackgroundOpacity: Convert.ToDouble(v.GetValueOrDefault(SettingKeys.Site.Home.Background.Opacity) ?? 1.0),
        BannerContent: v.GetValueOrDefault(SettingKeys.Site.Home.Banner.Content) as string ?? "",
        BannerDisplayMode: v.GetValueOrDefault(SettingKeys.Site.Home.Banner.DisplayMode) as string ?? "full"
    );
}