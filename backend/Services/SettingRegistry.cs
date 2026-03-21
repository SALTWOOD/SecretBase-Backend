using backend.Database.Models;
using backend.SourceGenerators;
using Supabase.Postgrest;

namespace backend.Services;

[GenerateSettingsTree]
public static partial class SettingRegistry
{
    public static readonly string[] Keys = [
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
        "site.home.background.blur:float",   // 虚化度
        "site.home.background.opacity:float", // 透明度

        // --- Home Banner ---
        "site.home.banner.content:string",
        "site.home.banner.display_mode:string" // full | mini | screen | hidden
    ];

    public static ISettingProvider? Provider { get; set; }
}


public class SettingProvider(Supabase.Client _supa) : ISettingProvider
{
    public async Task<T?> GetAsync<T>(string key, T? defaultValue = default)
    {
        var setting = await _supa.From<Setting>()
            .Where(it => it.Key == key)
            .Single();
        if  (setting is null) return defaultValue;
        return setting.GetValue<T>();
    }

    public async Task SetAsync<T>(string key, T value)
    {
        var setting = await _supa.From<Setting>()
            .Where(it => it.Key == key)
            .Single();
        setting?.SetValue(value);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await _supa.From<Setting>()
            .Filter("key", Constants.Operator.Equals, key)
            .Count(Constants.CountType.Exact, CancellationToken.None) > 0;
    }

    public async Task<Dictionary<string, object?>> GetByPrefixAsync(string prefix)
    {
        var settings = await _supa.From<Setting>()
            .Filter("key", Constants.Operator.Like, $"{prefix}%")
            .Get();
        return settings.Models.ToDictionary(
            static it => it.Key,
            static it => it.GetValue<object>()
        );
    }
}