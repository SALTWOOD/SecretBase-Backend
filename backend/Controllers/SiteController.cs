using backend.Database.Entities;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

public readonly record struct SeoMetaResponse(
    string Title,
    string Description,
    string? Keywords = null,
    string? OgTitle = null,
    string? OgDescription = null,
    string? OgImage = null,
    string TwitterCard = "summary_large_image",
    string Robots = "index, follow",
    Dictionary<string, string>? Extras = null
);

[Route("site")]
[Produces("application/json")]
public class SiteController(BaseServices deps) : BaseApiController(deps)
{
    [HttpGet("seo")]
    [ProducesResponseType<SeoMetaResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSeoAsync(CancellationToken ct = default)
    {
        var seoDict = await _db.Settings
            .Where(x => x.Key.StartsWith(SettingKeys.Site.Seo.Prefix))
            .ToDictionaryAsync(
                x => x.Key,
                x => x.Value,
                ct
            );

        string GetValue(string key) => seoDict.GetValueOrDefault(key).ThrowIfNull();

        SeoMetaResponse response = new SeoMetaResponse
        {
            Title = GetValue(SettingKeys.Site.Seo.Title),
            Description = GetValue(SettingKeys.Site.Seo.Description),
            Keywords = GetValue(SettingKeys.Site.Seo.Keywords),
            OgTitle = GetValue(SettingKeys.Site.Seo.OgTitle),
            OgDescription = GetValue(SettingKeys.Site.Seo.OgDescription),
            OgImage = GetValue(SettingKeys.Site.Seo.OgImage),
            TwitterCard = GetValue(SettingKeys.Site.Seo.TwitterCard),
            Robots = GetValue(SettingKeys.Site.Seo.Robots)
        };
        return Ok(response);
    }
}