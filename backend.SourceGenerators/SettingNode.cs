using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace backend.SourceGenerators;

public class SettingNode
{
    protected readonly string _key;
    public static ISettingProvider? GlobalProvider { get; set; }
    
    public SettingNode(string key) => _key = key;
    
    public async Task<object?> GetValueAsync(object? defaultValue = null)
        => GlobalProvider != null ? await GlobalProvider.GetAsync(_key, defaultValue) : defaultValue;

    public async Task SetValueAsync(object? value)
    {
        if (GlobalProvider is not null) await GlobalProvider.SetAsync(_key, value);
    }

    public async Task<bool> ExistsAsync()
        => GlobalProvider != null && await GlobalProvider.ExistsAsync(_key);

    public TaskAwaiter<object?> GetAwaiter() => GetValueAsync().GetAwaiter();
}

public class SettingNode<T>(string key) : SettingNode(key)
{
    public async Task<T?> GetValueAsync(T? defaultValue = default)
        => GlobalProvider != null ? await GlobalProvider.GetAsync(_key, defaultValue) : defaultValue;

    public async Task SetValueAsync(T value)
        => await GlobalProvider?.SetAsync(_key, value)!;

    public new TaskAwaiter<T?> GetAwaiter() => GetValueAsync().GetAwaiter();
}