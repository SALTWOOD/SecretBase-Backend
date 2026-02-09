using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using SqlSugar;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace backend.OAuth;

public class SqlSugarApplicationStore : IOpenIddictApplicationStore<OpenIddictSqlSugarApplication>
{
    private readonly ISqlSugarClient _db;
    public SqlSugarApplicationStore(ISqlSugarClient db) => _db = db;

    #region 核心持久化 (Persistence)
    public async ValueTask CreateAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken)
        => await _db.Insertable(application).ExecuteCommandAsync();

    public async ValueTask UpdateAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken)
        => await _db.Updateable(application).ExecuteCommandAsync();

    public async ValueTask DeleteAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken)
        => await _db.Deleteable(application).ExecuteCommandAsync();

    public ValueTask<OpenIddictSqlSugarApplication> InstantiateAsync(CancellationToken cancellationToken)
        => new(new OpenIddictSqlSugarApplication());
    #endregion

    #region 查询逻辑 (Lookups)
    public async ValueTask<OpenIddictSqlSugarApplication?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
        => await _db.Queryable<OpenIddictSqlSugarApplication>().InSingleAsync(identifier);

    public async ValueTask<OpenIddictSqlSugarApplication?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
        => await _db.Queryable<OpenIddictSqlSugarApplication>().FirstAsync(it => it.ClientId == identifier);

    public async IAsyncEnumerable<OpenIddictSqlSugarApplication> FindByRedirectUriAsync(string uri, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 简单实现：全表查后在内存过滤，或者用 SQL 的 LIKE
        var apps = await _db.Queryable<OpenIddictSqlSugarApplication>().ToListAsync();
        foreach (var app in apps.Where(a => !string.IsNullOrEmpty(a.RedirectUris) && a.RedirectUris.Contains(uri)))
            yield return app;
    }

    public async IAsyncEnumerable<OpenIddictSqlSugarApplication> FindByPostLogoutRedirectUriAsync(string uri, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var apps = await _db.Queryable<OpenIddictSqlSugarApplication>().ToListAsync();
        foreach (var app in apps.Where(a => !string.IsNullOrEmpty(a.PostLogoutRedirectUris) && a.PostLogoutRedirectUris.Contains(uri)))
            yield return app;
    }
    #endregion

    #region 属性获取 (Getters)
    public ValueTask<string?> GetIdAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken) => new(application.Id);
    public ValueTask<string?> GetClientIdAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken) => new(application.ClientId);
    public ValueTask<string?> GetClientSecretAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken) => new(application.ClientSecret);
    public ValueTask<string?> GetClientTypeAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken) => new(application.ClientType);
    public ValueTask<string?> GetApplicationTypeAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken) => new(application.ApplicationType);
    public ValueTask<string?> GetConsentTypeAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken) => new(application.ConsentType);
    public ValueTask<string?> GetDisplayNameAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken) => new(application.DisplayName);

    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken)
        => new(DeserializeList(application.Permissions));

    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken)
        => new(DeserializeList(application.RedirectUris));

    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken)
        => new(DeserializeList(application.PostLogoutRedirectUris));

    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken)
        => new(DeserializeList(application.Requirements));
    #endregion

    #region 属性设置 (Setters)
    public ValueTask SetClientIdAsync(OpenIddictSqlSugarApplication application, string? identifier, CancellationToken cancellationToken) { application.ClientId = identifier; return default; }
    public ValueTask SetClientSecretAsync(OpenIddictSqlSugarApplication application, string? secret, CancellationToken cancellationToken) { application.ClientSecret = secret; return default; }
    public ValueTask SetClientTypeAsync(OpenIddictSqlSugarApplication application, string? type, CancellationToken cancellationToken) { application.ClientType = type; return default; }
    public ValueTask SetApplicationTypeAsync(OpenIddictSqlSugarApplication application, string? type, CancellationToken cancellationToken) { application.ApplicationType = type; return default; }
    public ValueTask SetConsentTypeAsync(OpenIddictSqlSugarApplication application, string? type, CancellationToken cancellationToken) { application.ConsentType = type; return default; }
    public ValueTask SetDisplayNameAsync(OpenIddictSqlSugarApplication application, string? name, CancellationToken cancellationToken) { application.DisplayName = name; return default; }

    public ValueTask SetPermissionsAsync(OpenIddictSqlSugarApplication application, ImmutableArray<string> permissions, CancellationToken cancellationToken)
    { application.Permissions = JsonSerializer.Serialize(permissions); return default; }

    public ValueTask SetRedirectUrisAsync(OpenIddictSqlSugarApplication application, ImmutableArray<string> uris, CancellationToken cancellationToken)
    { application.RedirectUris = JsonSerializer.Serialize(uris); return default; }

    public ValueTask SetPostLogoutRedirectUrisAsync(OpenIddictSqlSugarApplication application, ImmutableArray<string> uris, CancellationToken cancellationToken)
    { application.PostLogoutRedirectUris = JsonSerializer.Serialize(uris); return default; }

    public ValueTask SetRequirementsAsync(OpenIddictSqlSugarApplication application, ImmutableArray<string> requirements, CancellationToken cancellationToken)
    { application.Requirements = JsonSerializer.Serialize(requirements); return default; }
    #endregion

    #region 复杂查询与未实现部分
    public async ValueTask<long> CountAsync(CancellationToken cancellationToken) => Convert.ToInt64(await _db.Queryable<OpenIddictSqlSugarApplication>().CountAsync());

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<OpenIddictSqlSugarApplication>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        => throw new NotSupportedException("LINQ to SugarQueryable conversion not supported. Use business logic instead.");

    public async IAsyncEnumerable<OpenIddictSqlSugarApplication> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.Queryable<OpenIddictSqlSugarApplication>();
        if (offset.HasValue) query.Skip(offset.Value);
        if (count.HasValue) query.Take(count.Value);
        foreach (var app in await query.ToListAsync()) yield return app;
    }

    // 辅助方法：处理 JSON 反序列化
    private static ImmutableArray<string> DeserializeList(string? json) =>
        string.IsNullOrEmpty(json) ? ImmutableArray<string>.Empty : JsonSerializer.Deserialize<string[]>(json)!.ToImmutableArray();

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken)
    {
        // 即使没有多语言，也要返回空字典而不是异常喵
        return new ValueTask<ImmutableDictionary<CultureInfo, string>>(ImmutableDictionary<CultureInfo, string>.Empty);
    }

    public ValueTask SetDisplayNamesAsync(OpenIddictSqlSugarApplication application, ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
    {
        // 如果不需要多语言，这里留空即可
        return default;
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken)
    {
        // 关键：OpenIddict 会频繁检查扩展属性，必须返回空字典
        if (string.IsNullOrEmpty(application.Properties))
        {
            return new ValueTask<ImmutableDictionary<string, JsonElement>>(ImmutableDictionary<string, JsonElement>.Empty);
        }

        var properties = JsonSerializer.Deserialize<ImmutableDictionary<string, JsonElement>>(application.Properties);
        return new ValueTask<ImmutableDictionary<string, JsonElement>>(properties ?? ImmutableDictionary<string, JsonElement>.Empty);
    }

    public ValueTask SetPropertiesAsync(OpenIddictSqlSugarApplication application, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        application.Properties = properties is { Count: > 0 }
            ? JsonSerializer.Serialize(properties)
            : null;
        return default;
    }

    public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken)
    {
        // 某些高级配置会存放在这里，也需要返回空字典
        return new ValueTask<ImmutableDictionary<string, string>>(ImmutableDictionary<string, string>.Empty);
    }

    public ValueTask SetSettingsAsync(OpenIddictSqlSugarApplication application, ImmutableDictionary<string, string> settings, CancellationToken cancellationToken)
    {
        // 留空即可喵
        return default;
    }

    public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(OpenIddictSqlSugarApplication application, CancellationToken cancellationToken)
    {
        // 除非你在做极其复杂的 JWT 签名校验，否则返回 null 是安全的
        return new ValueTask<JsonWebKeySet?>((JsonWebKeySet?)null);
    }

    public ValueTask SetJsonWebKeySetAsync(OpenIddictSqlSugarApplication application, JsonWebKeySet? set, CancellationToken cancellationToken)
    {
        return default;
    }

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<OpenIddictSqlSugarApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<OpenIddictSqlSugarApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
    #endregion
}