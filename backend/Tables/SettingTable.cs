using SqlSugar;
using Newtonsoft.Json;

namespace backend.Tables;

public enum SettingType
{
    String = 0,
    Number = 1,
    Boolean = 2,
    Json = 3
}

[SugarTable("settings")]
public class SettingTable
{
    [SugarColumn(IsPrimaryKey = true)]
    public required string Key { get; set; }

    [SugarColumn(ColumnDataType = "varchar(max)")]
    public string? Value { get; set; }

    public SettingType Type { get; set; } = SettingType.String;

    public string? Description { get; set; }

    public T? GetValue<T>()
    {
        if (string.IsNullOrEmpty(Value)) return default;

        if (Type == SettingType.Json)
        {
            return JsonConvert.DeserializeObject<T>(Value);
        }

        try
        {
            return (T)Convert.ChangeType(Value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    public void SetValue(object val)
    {
        if (val is string s)
        {
            Value = s;
            Type = SettingType.String;
        }
        else if (val is bool b)
        {
            Value = b.ToString().ToLower();
            Type = SettingType.Boolean;
        }
        else if (val is int || val is long || val is double)
        {
            Value = val.ToString();
            Type = SettingType.Number;
        }
        else
        {
            Value = JsonConvert.SerializeObject(val);
            Type = SettingType.Json;
        }
    }
}