using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace backend.SourceGenerators;

public class SettingNode
{
    protected readonly string _key;
    public static ISettingProvider? Provider { get; set; }
    
    public SettingNode(string key) => _key = key;
    
    public async Task<object?> GetValueAsync(object? defaultValue = null)
        => Provider != null ? await Provider.GetAsync(_key, defaultValue) : defaultValue;

    public async Task SetValueAsync(object? value)
    {
        if (Provider is not null) await Provider.SetAsync(_key, value);
    }

    public async Task<bool> ExistsAsync()
        => Provider != null && await Provider.ExistsAsync(_key);

    public TaskAwaiter<object?> GetAwaiter() => GetValueAsync().GetAwaiter();
}

public class SettingNode<T>(string key) : SettingNode(key)
{
    public async Task<T?> GetValueAsync(T? defaultValue = default)
        => Provider != null ? await Provider.GetAsync(_key, defaultValue) : defaultValue;

    public async Task SetValueAsync(T value)
        => await Provider?.SetAsync(_key, value)!;

    public new TaskAwaiter<T?> GetAwaiter() => GetValueAsync().GetAwaiter();
}