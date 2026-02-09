using OpenIddict.Abstractions;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SqlSugar;

namespace backend.OAuth;

public class SqlSugarTokenStore : IOpenIddictTokenStore<OpenIddictSqlSugarToken>
{
    private readonly ISqlSugarClient _db;

    public SqlSugarTokenStore(ISqlSugarClient db)
    {
        _db = db;
    }

    #region 基础增删改查 (Core Actions)

    public ValueTask CreateAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask(_db.Insertable(token).ExecuteCommandAsync());
    }

    public ValueTask UpdateAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask(_db.Updateable(token).ExecuteCommandAsync());
    }

    public ValueTask DeleteAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask(_db.Deleteable(token).ExecuteCommandAsync());
    }

    public async ValueTask<OpenIddictSqlSugarToken?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _db.Queryable<OpenIddictSqlSugarToken>()
            .InSingleAsync(identifier);
    }

    public async ValueTask<OpenIddictSqlSugarToken?> FindByReferenceIdAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _db.Queryable<OpenIddictSqlSugarToken>()
            .FirstAsync(it => it.ReferenceId == identifier);
    }

    #endregion

    #region 集合查询 (Query Actions)

    public async IAsyncEnumerable<OpenIddictSqlSugarToken> FindAsync(
        string? subject, string? client, string? status, string? type, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.Queryable<OpenIddictSqlSugarToken>();
        if (!string.IsNullOrEmpty(subject)) query.Where(it => it.Subject == subject);
        if (!string.IsNullOrEmpty(client)) query.Where(it => it.ApplicationId == client);
        if (!string.IsNullOrEmpty(status)) query.Where(it => it.Status == status);
        if (!string.IsNullOrEmpty(type)) query.Where(it => it.Type == type);

        var results = await query.ToListAsync();
        foreach (var result in results) yield return result;
    }

    public async IAsyncEnumerable<OpenIddictSqlSugarToken> FindByApplicationIdAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var results = await _db.Queryable<OpenIddictSqlSugarToken>().Where(it => it.ApplicationId == identifier).ToListAsync();
        foreach (var result in results) yield return result;
    }

    public async IAsyncEnumerable<OpenIddictSqlSugarToken> FindByAuthorizationIdAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var results = await _db.Queryable<OpenIddictSqlSugarToken>().Where(it => it.AuthorizationId == identifier).ToListAsync();
        foreach (var result in results) yield return result;
    }

    public async IAsyncEnumerable<OpenIddictSqlSugarToken> FindBySubjectAsync(string subject, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var results = await _db.Queryable<OpenIddictSqlSugarToken>().Where(it => it.Subject == subject).ToListAsync();
        foreach (var result in results) yield return result;
    }

    #endregion

    #region 属性操作 (Getter/Setter)

    public ValueTask<string?> GetIdAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken) => new(token.Id);
    public ValueTask<string?> GetApplicationIdAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken) => new(token.ApplicationId);
    public ValueTask SetApplicationIdAsync(OpenIddictSqlSugarToken token, string? identifier, CancellationToken cancellationToken) { token.ApplicationId = identifier; return default; }

    public ValueTask<string?> GetAuthorizationIdAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken) => new(token.AuthorizationId);
    public ValueTask SetAuthorizationIdAsync(OpenIddictSqlSugarToken token, string? identifier, CancellationToken cancellationToken) { token.AuthorizationId = identifier; return default; }

    public ValueTask<string?> GetStatusAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken) => new(token.Status);
    public ValueTask SetStatusAsync(OpenIddictSqlSugarToken token, string? status, CancellationToken cancellationToken) { token.Status = status; return default; }

    public ValueTask<string?> GetTypeAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken) => new(token.Type);
    public ValueTask SetTypeAsync(OpenIddictSqlSugarToken token, string? type, CancellationToken cancellationToken) { token.Type = type; return default; }

    public ValueTask<string?> GetSubjectAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken) => new(token.Subject);
    public ValueTask SetSubjectAsync(OpenIddictSqlSugarToken token, string? subject, CancellationToken cancellationToken) { token.Subject = subject; return default; }

    public ValueTask<string?> GetReferenceIdAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken) => new(token.ReferenceId);
    public ValueTask SetReferenceIdAsync(OpenIddictSqlSugarToken token, string? identifier, CancellationToken cancellationToken) { token.ReferenceId = identifier; return default; }

    public ValueTask<string?> GetPayloadAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken) => new(token.Payload);
    public ValueTask SetPayloadAsync(OpenIddictSqlSugarToken token, string? payload, CancellationToken cancellationToken) { token.Payload = payload; return default; }

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken) => new(token.CreationDate);
    public ValueTask SetCreationDateAsync(OpenIddictSqlSugarToken token, DateTimeOffset? date, CancellationToken cancellationToken) { token.CreationDate = date?.UtcDateTime; return default; }

    public ValueTask<DateTimeOffset?> GetExpirationDateAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken) => new(token.ExpirationDate);
    public ValueTask SetExpirationDateAsync(OpenIddictSqlSugarToken token, DateTimeOffset? date, CancellationToken cancellationToken) { token.ExpirationDate = date?.UtcDateTime; return default; }

    public ValueTask<DateTimeOffset?> GetRedemptionDateAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken) => new(token.RedemptionDate);
    public ValueTask SetRedemptionDateAsync(OpenIddictSqlSugarToken token, DateTimeOffset? date, CancellationToken cancellationToken) { token.RedemptionDate = date?.UtcDateTime; return default; }

    #endregion

    #region JSON 属性与高级功能

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(OpenIddictSqlSugarToken token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token.Properties)) return new(ImmutableDictionary<string, JsonElement>.Empty);
        return new(JsonSerializer.Deserialize<ImmutableDictionary<string, JsonElement>>(token.Properties) ?? ImmutableDictionary<string, JsonElement>.Empty);
    }

    public ValueTask SetPropertiesAsync(OpenIddictSqlSugarToken token, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken)
    {
        token.Properties = (properties is null || properties.IsEmpty) ? null : JsonSerializer.Serialize(properties);
        return default;
    }

    public ValueTask<OpenIddictSqlSugarToken> InstantiateAsync(CancellationToken cancellationToken)
        => new(new OpenIddictSqlSugarToken { Id = Guid.NewGuid().ToString() });

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
        => await _db.Queryable<OpenIddictSqlSugarToken>().CountAsync();

    public async IAsyncEnumerable<OpenIddictSqlSugarToken> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = _db.Queryable<OpenIddictSqlSugarToken>();
        if (offset.HasValue) query.Skip(offset.Value);
        if (count.HasValue) query.Take(count.Value);
        var results = await query.ToListAsync();
        foreach (var result in results) yield return result;
    }

    #endregion

    #region 撤销与清理 (Revoke & Prune)

    public async ValueTask<long> RevokeAsync(string? subject, string? client, string? status, string? type, CancellationToken cancellationToken)
    {
        var query = _db.Updateable<OpenIddictSqlSugarToken>().SetColumns(it => it.Status, OpenIddictConstants.Statuses.Revoked);
        if (!string.IsNullOrEmpty(subject)) query.Where(it => it.Subject == subject);
        if (!string.IsNullOrEmpty(client)) query.Where(it => it.ApplicationId == client);
        if (!string.IsNullOrEmpty(status)) query.Where(it => it.Status == status);
        if (!string.IsNullOrEmpty(type)) query.Where(it => it.Type == type);
        return await query.ExecuteCommandAsync();
    }

    public ValueTask<long> RevokeByApplicationIdAsync(string identifier, CancellationToken cancellationToken = default) => RevokeAsync(null, identifier, null, null, cancellationToken);
    public ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken = default) => RevokeAsync(subject, null, null, null, cancellationToken);
    public async ValueTask<long> RevokeByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken)
    {
        return await _db.Updateable<OpenIddictSqlSugarToken>()
            .SetColumns(it => it.Status, OpenIddictConstants.Statuses.Revoked)
            .Where(it => it.AuthorizationId == identifier)
            .ExecuteCommandAsync();
    }

    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        // 清理已兑换、已撤销或已过期的令牌
        return await _db.Deleteable<OpenIddictSqlSugarToken>()
            .Where(it => it.CreationDate < threshold.UtcDateTime)
            .Where(it => it.Status != OpenIddictConstants.Statuses.Valid ||
                         it.ExpirationDate < DateTime.UtcNow ||
                         it.RedemptionDate != null)
            .ExecuteCommandAsync();
    }

    #endregion

    #region IQueryable (Not Supported)
    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<OpenIddictSqlSugarToken>, IQueryable<TResult>> query, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<OpenIddictSqlSugarToken>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) => throw new NotSupportedException();
    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<OpenIddictSqlSugarToken>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) => throw new NotSupportedException();
    #endregion
}