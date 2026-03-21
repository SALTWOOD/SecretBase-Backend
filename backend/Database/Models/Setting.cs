using System.Text.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace backend.Database.Models;

public enum SettingType
{
    String = 0,
    Number = 1,
    Boolean = 2,
    Float = 3,
    Json = 4,
    Null = 5,
}

[Table("settings")]
public class Setting : BaseModel
{
    [PrimaryKey("key", false)]
    public string Key { get; set; } = string.Empty;

    [Column("value")]
    public string? Value { get; set; }
    
    [Column("type")]
    public SettingType? Type { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public T? GetValue<T>()
    {
        if (string.IsNullOrEmpty(Value)) return default;

        Type targetType = typeof(T);
        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(object) || underlyingType == typeof(string))
        {
            if (Type == SettingType.String || Type == SettingType.Null)
            {
                return (T?)(object?)Value;
            }
        }

        if (Type == SettingType.Json) return JsonSerializer.Deserialize<T>(Value);

        if (underlyingType.IsEnum) return (T)Enum.Parse(underlyingType, Value);

        return (T)Convert.ChangeType(Value, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
    }


    public void SetValue(object? val)
    {
        if (val == null)
        {
            Value = null;
            Type = SettingType.Null;
            return;
        }

        switch (val)
        {
            case string s:
                Value = s;
                Type = SettingType.String;
                break;
            case sbyte:
            case byte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
            case ulong:
                Value = Convert.ToString(val, System.Globalization.CultureInfo.InvariantCulture);
                Type = SettingType.Number;
                break;
            case float:
            case double:
            case decimal:
                Value = Convert.ToString(val, System.Globalization.CultureInfo.InvariantCulture);
                Type = SettingType.Float;
                break;
            case bool b:
                Value = b ? "true" : "false";
                Type = SettingType.Boolean;
                break;
            default:
                Value = JsonSerializer.Serialize(val);
                Type = SettingType.Json;
                break;
        }
    }

    public override string ToString()
    {
        return GetValue<object>()?.ToString() ?? "null";
    }
}