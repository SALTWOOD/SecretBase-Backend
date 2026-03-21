using backend.Database.Entities;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Frozen;

namespace backend.Controllers;

// --- Response DTOs ---

// public readonly record struct HomeAppearanceResponse(
//     string BackgroundUrl,
//     int BackgroundBlur,
//     double BackgroundOpacity,
//     string BannerContent,
//     string BannerDisplayMode
// );
//
// public readonly record struct SeoMetaResponse(
//     string Title,
//     string Description,
//     string? Keywords = null,
//     string? OgTitle = null,
//     string? OgDescription = null,
//     string? OgImage = null,
//     string TwitterCard = "summary_large_image",
//     string Robots = "index, follow"
// );

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
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSeo()
    {
        return Ok(await SettingRegistry.Site.Seo.GetValuesAsync());
    }

    [HttpGet("home")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetHomeAppearance()
    {
        return Ok(await SettingRegistry.Site.Home.GetValuesAsync());
    }

    [HttpGet("init")]
    [ProducesResponseType(typeof(SiteInitResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SiteInitResponse>> GetSiteInit()
    {
        return Ok(new SiteInitResponse(
            Seo: await SettingRegistry.Site.Seo.GetValuesAsync(),
            Home: await SettingRegistry.Site.Home.GetValuesAsync(),
            RegistrationEnabled: await SettingRegistry.Site.User.Registration.Enabled
        ));
    }
}