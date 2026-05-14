using backend.Database;
using backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using backend.SourceGenerators;

namespace backend.Services;

public class EfSettingProvider(IServiceScopeFactory scopeFactory) : ISettingProvider
{
    public async Task<T?> GetAsync<T>(string key, T? defaultValue = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity = await db.Set<Setting>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key);

        return entity is null ? defaultValue : entity.GetValue<T>();
    }

    public async Task SetAsync<T>(string key, T value)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Create a temporary Setting to compute the serialized value and type
        var temp = new Setting();
        temp.SetValue(value);
        var serializedValue = temp.Value;
        var settingType = temp.Type;

        // Try atomic update first
        var rowsAffected = await db.Set<Setting>()
            .Where(x => x.Key == key)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Value, serializedValue)
                .SetProperty(x => x.Type, settingType));

        // If no existing row, insert new
        if (rowsAffected == 0)
        {
            var newSetting = new Setting { Key = key };
            newSetting.SetValue(value);
            db.Set<Setting>().Add(newSetting);
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Set<Setting>().AnyAsync(x => x.Key == key);
    }

    public async Task<IDictionary<string, object?>> GetValuesByPrefixAsync(string prefix)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var settings = await db.Set<Setting>()
            .AsNoTracking()
            .Where(x => x.Key.StartsWith(prefix))
            .ToListAsync();

        return settings.ToDictionary(
            x => x.Key,
            x => x.GetValue<object?>()
        );
    }
}