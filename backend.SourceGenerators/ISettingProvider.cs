using System.Threading.Tasks;

namespace backend.SourceGenerators;

public interface ISettingProvider
{
    // 获取配置：支持 string, int, bool, float 等
    Task<T?> GetAsync<T>(string key, T? defaultValue = default);
    
    // 写入配置
    Task SetAsync<T>(string key, T value);
}