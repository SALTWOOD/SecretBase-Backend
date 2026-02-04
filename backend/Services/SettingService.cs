using backend.Tables;
using SqlSugar;
using System.Collections.Concurrent;

namespace backend.Services;

public class SettingService(ISqlSugarClient db)
{
    private static readonly ConcurrentDictionary<string, object?> _cache = new();

    public async Task<T?> Get<T>(string key)
    {
        if (_cache.TryGetValue(key, out var cachedValue))
            return (T?)cachedValue;

        var setting = await db.Queryable<SettingTable>()
            .Where(it => it.Key == key)
            .FirstAsync();

        var value = setting == null ? default : setting.GetValue<T>();

        _cache[key] = value;

        return value;
    }

    public async Task Set<T>(string key, T value)
    {
        await db.Storageable(new SettingTable
        {
            Key = key,
            Value = value?.ToString()
        })
        .ExecuteCommandAsync();

        _cache[key] = value;
    }

    public void ClearCache() => _cache.Clear();
}