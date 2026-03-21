using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.SourceGenerators;

public class SettingNode
{
    protected readonly string _key;
    public string Key => _key;
    public static ISettingProvider? Provider  { get; set; }

    public SettingNode(string key)
    {
        _key = key;
    }
    
    public async Task<object?> GetValueAsync(object? defaultValue = default) 
        => Provider != null ? await Provider.GetAsync(_key, defaultValue) : defaultValue;

    public async Task SetValueAsync(object value) 
        => await Provider?.SetAsync(_key, value)!;

    public async Task<bool> ExistsAsync()
    {
        if (Provider == null) return false;
        return await Provider.ExistsAsync(_key);
    }

    public async Task<Dictionary<string, object?>> GetValuesAsync()
    {
        if (Provider == null) return new Dictionary<string, object?>();
        return await Provider.GetByPrefixAsync(_key);
    }
}

public class SettingNode<T>(ISettingProvider provider, string key) : SettingNode(provider, key)
{
    public async Task<T?> GetValueAsync(T? defaultValue = default) 
        => Provider != null ? await Provider.GetAsync(_key, defaultValue) : defaultValue;

    public async Task SetValueAsync(T value) 
        => await Provider?.SetAsync(_key, value)!;
}