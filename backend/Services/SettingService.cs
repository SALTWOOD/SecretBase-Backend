using backend.Tables;
using SqlSugar;
using System.Collections.Concurrent;
using System.Text.Json;

namespace backend.Services;

public class SettingService(ISqlSugarClient db)
{
    private static readonly ConcurrentDictionary<string, object?> _cache = new();

    public async ValueTask<T?> Get<T>(string key)
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
            Value = JsonSerializer.Serialize(value)
        })
        .DefaultAddElseUpdate()
        .ExecuteCommandAsync();

        _cache[key] = value;
    }

    public async Task<bool> Exists(string key)
    {
        if (_cache.ContainsKey(key)) return true;
        var val = await Get<object>(key);
        return val != null;
    }

    public async Task Delete(string key)
    {
        await db.Deleteable<SettingTable>()
            .Where(it => it.Key == key)
            .ExecuteCommandAsync();

        _cache.TryRemove(key, out _);
    }
}