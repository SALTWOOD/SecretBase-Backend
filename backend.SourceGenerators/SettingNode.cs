using System.Threading.Tasks;

namespace backend.SourceGenerators;

public class SettingNode<T>
{
    private readonly string _key;
    public static ISettingProvider? Provider { get; set; }

    public SettingNode(string key) => _key = key;

    public async Task<T?> GetValueAsync(T? defaultValue = default) 
        => Provider != null ? await Provider.GetAsync(_key, defaultValue) : defaultValue;

    public async Task SetValueAsync(T value) 
        => await Provider?.SetAsync(_key, value)!;
}