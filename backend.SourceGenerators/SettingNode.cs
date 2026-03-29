using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace backend.SourceGenerators;

public class SettingNode
{
    protected readonly string _key;
    protected readonly object? _defaultValue;
    public static ISettingProvider? GlobalProvider { get; set; }

    public SettingNode(string key, object? defaultValue = null)
    {
        _key = key;
        _defaultValue = defaultValue;
    }

    public async Task<object?> GetValueAsync(object? defaultValue = null)
    {
        if (GlobalProvider == null) return defaultValue ?? _defaultValue;
        var result = await GlobalProvider.GetAsync(_key, defaultValue);
        return result ?? _defaultValue ?? defaultValue;
    }

    public async Task SetValueAsync(object? value)
    {
        if (GlobalProvider is not null) await GlobalProvider.SetAsync(_key, value);
    }

    public async Task<bool> ExistsAsync()
    {
        return GlobalProvider != null && await GlobalProvider.ExistsAsync(_key);
    }

    public TaskAwaiter<object?> GetAwaiter()
    {
        return GetValueAsync().GetAwaiter();
    }
}

public class SettingNode<T>(string key, T? defaultValue = default) : SettingNode(key, defaultValue)
{
    public new async Task<T?> GetValueAsync(T? overrideDefaultValue = default)
    {
        if (GlobalProvider == null) return _defaultValue is T dv ? dv : overrideDefaultValue;
        var result = await GlobalProvider.GetAsync<T>(_key, overrideDefaultValue);
        return result is not null ? result : (_defaultValue is T builtinDv ? builtinDv : overrideDefaultValue);
    }

    public new async Task SetValueAsync(T value)
    {
        await GlobalProvider?.SetAsync(_key, value)!;
    }

    public new TaskAwaiter<T?> GetAwaiter()
    {
        return GetValueAsync().GetAwaiter();
    }
}