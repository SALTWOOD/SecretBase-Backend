using backend.Database.Entities;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Frozen;

namespace backend.Controllers;

// --- Response DTOs ---

public readonly record struct HomeBackgroundResponse(
    string Url,
    int Blur,
    int Opacity
);

public readonly record struct HomeBannerResponse(
    string Content,
    string DisplayMode
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
    IDictionary<string, object?> Seo,
    IDictionary<string, object?> Home,
    bool RegistrationEnabled
);

// --- Controller ---

[Route("settings")]
public class SettingsController(BaseServices deps) : BaseApiController(deps)
{
    [HttpGet("seo")]
    [ProducesResponseType<SettingRegistry.Site.Seo.SeoSettings>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSeo()
    {
        return Ok(await SettingRegistry.Site.Seo.General.GetValuesAsObjectAsync());
    }

    [HttpGet("home/background")]
    [ProducesResponseType<SettingRegistry.Site.Home.Background.HomeBackgroundSettings>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetHomeBackground()
    {
        return Ok(await SettingRegistry.Site.Home.Background.GetValuesAsObjectAsync());
    }

    [HttpGet("home/banner")]
    [ProducesResponseType<SettingRegistry.Site.Home.Banner.HomeBannerSettings>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetHomeBanner()
    {
        return Ok(await SettingRegistry.Site.Home.Banner.GetValuesAsObjectAsync());
    }

    [HttpGet("footer")]
    [ProducesResponseType<SettingRegistry.Site.Footer.FooterSettings>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFooter()
    {
        return Ok(await SettingRegistry.Site.Footer.GetValuesAsObjectAsync());
    }
}