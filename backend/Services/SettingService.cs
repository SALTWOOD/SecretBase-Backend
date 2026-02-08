using backend.Tables;
using SqlSugar;
using System.Collections.Concurrent;
using System.Text.Json;

namespace backend.Services;

public class SettingService(ISqlSugarClient db)
{
    private static readonly ConcurrentDictionary<string, SettingTable?> _cache = new();

    public async ValueTask<T?> Get<T>(string key)
    {
        if (_cache.TryGetValue(key, out var cached))
        {
            if (cached == null || cached.Type == SettingType.Null) return default;
            return cached.GetValue<T>();
        }

        var setting = await db.Queryable<SettingTable>()
            .Where(it => it.Key == key)
            .FirstAsync();

        if (setting == null)
        {
            var nullSetting = new SettingTable { Key = key, Type = SettingType.Null, Value = null };
            _cache[key] = nullSetting;
            return default;
        }

        _cache[key] = setting;
        return setting.GetValue<T>();
    }

    public async Task Set<T>(string key, T? value)
    {
        var setting = new SettingTable { Key = key };
        setting.SetValue(value!);

        await db.Storageable(setting)
            .DefaultAddElseUpdate()
            .ExecuteCommandAsync();

        _cache[key] = setting;
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