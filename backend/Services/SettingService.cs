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
        var val = await Get<object>(key);
        return val != null;
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
}