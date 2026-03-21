using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

// --- Response DTOs ---

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
            RegistrationEnabled: await SettingRegistry.Site.User.Registration.Enabled.GetValueAsync()
        ));
    }
}