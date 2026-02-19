using backend.Database;
using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace backend.Services;

public class SettingService(AppDbContext db)
{
    private static readonly ConcurrentDictionary<string, Setting?> _cache = new();

    public async ValueTask<T?> Get<T>(string key)
    {
        if (_cache.TryGetValue(key, out var cached))
        {
            if (cached == null || cached.Type == SettingType.Null) return default;
            return cached.GetValue<T>();
        }

        var setting = await db.Settings
            .FirstOrDefaultAsync(it => it.Key == key);

        if (setting == null)
        {
            var nullSetting = new Setting { Key = key, Type = SettingType.Null, Value = null };
            _cache[key] = nullSetting;
            return default;
        }

        _cache[key] = setting;
        return setting.GetValue<T>();
    }

    public object? GetRawValue(Setting setting)
    {
        return setting.Type switch
        {
            SettingType.String => setting.GetValue<string>(),
            SettingType.Number => setting.GetValue<double>(),
            SettingType.Boolean => setting.GetValue<bool>(),
            SettingType.Json => setting.GetValue<object>(),
            SettingType.Null => null,
            _ => null
        };
    }

    public async Task Set<T>(string key, T? value)
    {
        var setting = new Setting { Key = key };
        setting.SetValue(value!);

        var existing = await db.Settings.FindAsync(key);
        if (existing != null)
        {
            existing.Value = setting.Value;
            existing.Type = setting.Type;
            db.Settings.Update(existing);
        }
        else
        {
            await db.Settings.AddAsync(setting);
        }

        await db.SaveChangesAsync();

        _cache[key] = setting;
    }

    public async Task<bool> Exists(string key)
    {
        if (_cache.ContainsKey(key)) return true;
        return await db.Settings.AnyAsync(it => it.Key == key);
    }

    public async Task Delete(string key)
    {
        var setting = await db.Settings.FindAsync(key);
        if (setting != null)
        {
            db.Settings.Remove(setting);
            await db.SaveChangesAsync();
        }

        _cache.TryRemove(key, out _);
    }

    public async Task<Dictionary<string, object?>> GetBatch(IEnumerable<string> keys)
    {
        var result = new Dictionary<string, object?>();
        var keysToFetch = new List<string>();

        // 1. Try to get from cache first
        foreach (var key in keys)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                // If cached as Null, we treat it as null/default
                result[key] = (cached == null || cached.Type == SettingType.Null)
                    ? null
                    : cached.GetValue<object>();
            }
            else
            {
                keysToFetch.Add(key);
            }
        }

        // 2. Fetch missing keys from Database in one shot
        if (keysToFetch.Count != 0)
        {
            var settingsFromDb = await db.Settings
                .Where(it => keysToFetch.Contains(it.Key))
                .ToListAsync();

            var dbResultMap = settingsFromDb.ToDictionary(s => s.Key);

            foreach (var key in keysToFetch)
            {
                if (dbResultMap.TryGetValue(key, out var setting))
                {
                    _cache[key] = setting;
                    result[key] = GetRawValue(setting);
                }
                else
                {
                    _cache[key] = new Setting { Key = key, Type = SettingType.Null };
                    result[key] = null;
                }
            }
        }

        return result;
    }
}