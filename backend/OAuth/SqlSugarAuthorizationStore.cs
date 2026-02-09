using OpenIddict.Abstractions;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SqlSugar;

namespace backend.OAuth;

public class SqlSugarAuthorizationStore : IOpenIddictAuthorizationStore<OpenIddictSqlSugarAuthorization>
{
    private readonly ISqlSugarClient _db;

    public SqlSugarAuthorizationStore(ISqlSugarClient db)
    {
        _db = db;
    }

    #region 核心增删改查

    public ValueTask CreateAsync(OpenIddictSqlSugarAuthorization authorization, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask(_db.Insertable(authorization).ExecuteCommandAsync());
    }

    public ValueTask UpdateAsync(OpenIddictSqlSugarAuthorization authorization, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask(_db.Updateable(authorization).ExecuteCommandAsync());
    }

    public ValueTask DeleteAsync(OpenIddictSqlSugarAuthorization authorization, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask(_db.Deleteable(authorization).ExecuteCommandAsync());
    }

    public async ValueTask<OpenIddictSqlSugarAuthorization?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _db.Queryable<OpenIddictSqlSugarAuthorization>()
            .InSingleAsync(identifier);
    }

    public async IAsyncEnumerable<OpenIddictSqlSugarAuthorization> FindAsync(
        string? subject, string? client, string? status, string? type,
        ImmutableArray<string>? scopes, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.Queryable<OpenIddictSqlSugarAuthorization>();

        if (!string.IsNullOrEmpty(subject)) query.Where(it => it.Subject == subject);
        if (!string.IsNullOrEmpty(client)) query.Where(it => it.ApplicationId == client);
        if (!string.IsNullOrEmpty(status)) query.Where(it => it.Status == status);
        if (!string.IsNullOrEmpty(type)) query.Where(it => it.Type == type);

        var results = await query.ToListAsync();

        // 内存中过滤 Scopes (因为 Scopes 在实体中是以 JSON 字符串存储的)
        foreach (var result in results)
        {
            if (scopes is not { IsDefaultOrEmpty: false })
            {
                yield return result;
                continue;
            }

            var resultScopes = await GetScopesAsync(result, cancellationToken);
            if (scopes.Value.All(s => resultScopes.Contains(s)))
            {
                yield return result;
            }
        }
    }

    #endregion

    #region 属性获取与设置 (Getter/Setter)

    public ValueTask<string?> GetIdAsync(OpenIddictSqlSugarAuthorization authorization, CancellationToken cancellationToken)
        => new(authorization.Id);

    public ValueTask<string?> GetApplicationIdAsync(OpenIddictSqlSugarAuthorization authorization, CancellationToken cancellationToken)
        => new(authorization.ApplicationId);

    public ValueTask SetApplicationIdAsync(OpenIddictSqlSugarAuthorization authorization, string? identifier, CancellationToken cancellationToken)
    {
        authorization.ApplicationId = identifier;
        return default;
    }

    public ValueTask<string?> GetSubjectAsync(OpenIddictSqlSugarAuthorization authorization, CancellationToken cancellationToken)
        => new(authorization.Subject);

    public ValueTask SetSubjectAsync(OpenIddictSqlSugarAuthorization authorization, string? subject, CancellationToken cancellationToken)
    {
        authorization.Subject = subject;
        return default;
    }

    public ValueTask<string?> GetStatusAsync(OpenIddictSqlSugarAuthorization authorization, CancellationToken cancellationToken)
        => new(authorization.Status);

    public ValueTask SetStatusAsync(OpenIddictSqlSugarAuthorization authorization, string? status, CancellationToken cancellationToken)
    {
        authorization.Status = status;
        return default;
    }

    public ValueTask<string?> GetTypeAsync(OpenIddictSqlSugarAuthorization authorization, CancellationToken cancellationToken)
        => new(authorization.Type);

    public ValueTask SetTypeAsync(OpenIddictSqlSugarAuthorization authorization, string? type, CancellationToken cancellationToken)
    {
        authorization.Type = type;
        return default;
    }

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(OpenIddictSqlSugarAuthorization authorization, CancellationToken cancellationToken)
        => new(authorization.CreationDate);

    public ValueTask SetCreationDateAsync(OpenIddictSqlSugarAuthorization authorization, DateTimeOffset? date, CancellationToken cancellationToken)
    {
        authorization.CreationDate = date?.UtcDateTime;
        return default;
    }

    #endregion

    #region JSON 属性与 Scopes 处理

    public ValueTask<ImmutableArray<string>> GetScopesAsync(OpenIddictSqlSugarAuthorization authorization, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(authorization.Scopes))
            return new(ImmutableArray<string>.Empty);

        return new(JsonSerializer.Deserialize<string[]>(authorization.Scopes)?.ToImmutableArray() ?? ImmutableArray<string>.Empty);
    }

    public ValueTask SetScopesAsync(OpenIddictSqlSugarAuthorization authorization, ImmutableArray<string> scopes, CancellationToken cancellationToken)
    {
        authorization.Scopes = scopes.IsDefaultOrEmpty ? null : JsonSerializer.Serialize(scopes);
        return default;
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(OpenIddictSqlSugarAuthorization authorization, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(authorization.Properties))
            return new(ImmutableDictionary<string, JsonElement>.Empty);

        return new(JsonSerializer.Deserialize<ImmutableDictionary<string, JsonElement>>(authorization.Properties) ?? ImmutableDictionary<string, JsonElement>.Empty);
    }

    public ValueTask SetPropertiesAsync(OpenIddictSqlSugarAuthorization authorization, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        authorization.Properties = (properties is null || properties.IsEmpty) ? null : JsonSerializer.Serialize(properties);
        return default;
    }

    #endregion

    #region 其他必须实现的方法

    public ValueTask<OpenIddictSqlSugarAuthorization> InstantiateAsync(CancellationToken cancellationToken)
        => new(new OpenIddictSqlSugarAuthorization { Id = Guid.NewGuid().ToString() });

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
        => await _db.Queryable<OpenIddictSqlSugarAuthorization>().CountAsync();

    public async IAsyncEnumerable<OpenIddictSqlSugarAuthorization> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.Queryable<OpenIddictSqlSugarAuthorization>();
        if (offset.HasValue) query.Skip(offset.Value);
        if (count.HasValue) query.Take(count.Value);

        var results = await query.ToListAsync();
        foreach (var result in results) yield return result;
    }

    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        // 清理过期的、无效的授权记录
        return await _db.Deleteable<OpenIddictSqlSugarAuthorization>()
            .Where(it => it.CreationDate < threshold.UtcDateTime &&
                         (it.Status != OpenIddictConstants.Statuses.Valid || it.Type == OpenIddictConstants.AuthorizationTypes.AdHoc))
            .ExecuteCommandAsync();
    }

    // 处理 IQueryable 的方法在自定义 Store 中通常可以抛出异常，
    // 因为 OpenIddict 内部如果检测到你没提供这些底层 Queryable 支持，会回退到使用 FindByXXX 方法
    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<OpenIddictSqlSugarAuthorization>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        => throw new NotSupportedException("IQueryable is not supported in SqlSugar store.");

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<OpenIddictSqlSugarAuthorization>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
        => throw new NotSupportedException("IQueryable is not supported in SqlSugar store.");

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<OpenIddictSqlSugarAuthorization>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
        => throw new NotSupportedException("IQueryable is not supported in SqlSugar store.");

    #endregion

    #region 撤销 (Revoke) 逻辑

    public async ValueTask<long> RevokeAsync(string? subject, string? client, string? status, string? type, CancellationToken cancellationToken)
    {
        var query = _db.Updateable<OpenIddictSqlSugarAuthorization>()
                       .SetColumns(it => it.Status, OpenIddictConstants.Statuses.Revoked);

        if (!string.IsNullOrEmpty(subject)) query.Where(it => it.Subject == subject);
        if (!string.IsNullOrEmpty(client)) query.Where(it => it.ApplicationId == client);
        if (!string.IsNullOrEmpty(status)) query.Where(it => it.Status == status);
        if (!string.IsNullOrEmpty(type)) query.Where(it => it.Type == type);

        return await query.ExecuteCommandAsync();
    }

    public ValueTask<long> RevokeByApplicationIdAsync(string identifier, CancellationToken cancellationToken)
        => RevokeAsync(null, identifier, null, null, cancellationToken);

    public ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken)
        => RevokeAsync(subject, null, null, null, cancellationToken);

    public async IAsyncEnumerable<OpenIddictSqlSugarAuthorization> FindByApplicationIdAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var results = await _db.Queryable<OpenIddictSqlSugarAuthorization>().Where(it => it.ApplicationId == identifier).ToListAsync();
        foreach (var result in results) yield return result;
    }

    public async IAsyncEnumerable<OpenIddictSqlSugarAuthorization> FindBySubjectAsync(string subject, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var results = await _db.Queryable<OpenIddictSqlSugarAuthorization>().Where(it => it.Subject == subject).ToListAsync();
        foreach (var result in results) yield return result;
    }

    #endregion
}