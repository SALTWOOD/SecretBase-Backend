using OpenIddict.Abstractions;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SqlSugar;

namespace backend.OAuth;

public class SqlSugarScopeStore : IOpenIddictScopeStore<OpenIddictSqlSugarScope>
{
    private readonly ISqlSugarClient _db;

    public SqlSugarScopeStore(ISqlSugarClient db)
    {
        _db = db;
    }

    #region 核心增删改查 (Core Actions)

    public ValueTask CreateAsync(OpenIddictSqlSugarScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask(_db.Insertable(scope).ExecuteCommandAsync());
    }

    public ValueTask UpdateAsync(OpenIddictSqlSugarScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask(_db.Updateable(scope).ExecuteCommandAsync());
    }

    public ValueTask DeleteAsync(OpenIddictSqlSugarScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask(_db.Deleteable(scope).ExecuteCommandAsync());
    }

    public async ValueTask<OpenIddictSqlSugarScope?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _db.Queryable<OpenIddictSqlSugarScope>()
            .InSingleAsync(identifier);
    }

    public async ValueTask<OpenIddictSqlSugarScope?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _db.Queryable<OpenIddictSqlSugarScope>()
            .FirstAsync(it => it.Name == name);
    }

    public async IAsyncEnumerable<OpenIddictSqlSugarScope> FindByNamesAsync(ImmutableArray<string> names, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var nameList = names.ToList();

        var results = await _db.Queryable<OpenIddictSqlSugarScope>()
            .Where(it => nameList.Contains(it.Name))
            .ToListAsync();
        foreach (var result in results) yield return result;
    }

    public async IAsyncEnumerable<OpenIddictSqlSugarScope> FindByResourceAsync(string resource, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 因为 Resources 是 JSON 存储，使用数据库的 LIKE 或者 JSON 搜索
        // 这里为了通用性使用简洁实现，生产环境建议优化
        var results = await _db.Queryable<OpenIddictSqlSugarScope>()
            .Where(it => it.Resources!.Contains(resource))
            .ToListAsync();
        foreach (var result in results) yield return result;
    }

    #endregion

    #region 属性 Getter/Setter

    public ValueTask<string?> GetIdAsync(OpenIddictSqlSugarScope scope, CancellationToken cancellationToken) => new(scope.Id);
    public ValueTask<string?> GetNameAsync(OpenIddictSqlSugarScope scope, CancellationToken cancellationToken) => new(scope.Name);
    public ValueTask SetNameAsync(OpenIddictSqlSugarScope scope, string? name, CancellationToken cancellationToken) { scope.Name = name; return default; }

    public ValueTask<string?> GetDisplayNameAsync(OpenIddictSqlSugarScope scope, CancellationToken cancellationToken) => new(scope.DisplayName);
    public ValueTask SetDisplayNameAsync(OpenIddictSqlSugarScope scope, string? name, CancellationToken cancellationToken) { scope.DisplayName = name; return default; }

    public ValueTask<string?> GetDescriptionAsync(OpenIddictSqlSugarScope scope, CancellationToken cancellationToken) => new(scope.Description);
    public ValueTask SetDescriptionAsync(OpenIddictSqlSugarScope scope, string? description, CancellationToken cancellationToken) { scope.Description = description; return default; }

    #endregion

    #region JSON & 多语言处理

    public ValueTask<ImmutableArray<string>> GetResourcesAsync(OpenIddictSqlSugarScope scope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(scope.Resources)) return new(ImmutableArray<string>.Empty);
        return new(JsonSerializer.Deserialize<string[]>(scope.Resources)?.ToImmutableArray() ?? ImmutableArray<string>.Empty);
    }

    public ValueTask SetResourcesAsync(OpenIddictSqlSugarScope scope, ImmutableArray<string> resources, CancellationToken cancellationToken)
    {
        scope.Resources = resources.IsDefaultOrEmpty ? null : JsonSerializer.Serialize(resources);
        return default;
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(OpenIddictSqlSugarScope scope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(scope.Properties)) return new(ImmutableDictionary<string, JsonElement>.Empty);
        return new(JsonSerializer.Deserialize<ImmutableDictionary<string, JsonElement>>(scope.Properties) ?? ImmutableDictionary<string, JsonElement>.Empty);
    }

    public ValueTask SetPropertiesAsync(OpenIddictSqlSugarScope scope, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        scope.Properties = (properties is null || properties.IsEmpty) ? null : JsonSerializer.Serialize(properties);
        return default;
    }

    // 处理多语言名称和描述 (简单的 JSON 映射)
    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(OpenIddictSqlSugarScope scope, CancellationToken cancellationToken)
        => new(ImmutableDictionary<CultureInfo, string>.Empty); // 简化实现

    public ValueTask SetDisplayNamesAsync(OpenIddictSqlSugarScope scope, ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken)
        => default;

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(OpenIddictSqlSugarScope scope, CancellationToken cancellationToken)
        => new(ImmutableDictionary<CultureInfo, string>.Empty);

    public ValueTask SetDescriptionsAsync(OpenIddictSqlSugarScope scope, ImmutableDictionary<CultureInfo, string> descriptions, CancellationToken cancellationToken)
        => default;

    #endregion

    #region 其他接口实现

    public ValueTask<OpenIddictSqlSugarScope> InstantiateAsync(CancellationToken cancellationToken)
        => new(new OpenIddictSqlSugarScope { Id = Guid.NewGuid().ToString() });

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
        => await _db.Queryable<OpenIddictSqlSugarScope>().CountAsync();

    public async IAsyncEnumerable<OpenIddictSqlSugarScope> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.Queryable<OpenIddictSqlSugarScope>();
        if (offset.HasValue) query.Skip(offset.Value);
        if (count.HasValue) query.Take(count.Value);
        var results = await query.ToListAsync();
        foreach (var result in results) yield return result;
    }

    #endregion

    #region IQueryable (Not Supported)
    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<OpenIddictSqlSugarScope>, IQueryable<TResult>> query, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<OpenIddictSqlSugarScope>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) => throw new NotSupportedException();
    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<OpenIddictSqlSugarScope>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) => throw new NotSupportedException();
    #endregion
}