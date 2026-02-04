using Newtonsoft.Json;
using SqlSugar;

namespace backend.Tables;

public enum SettingType
{
    String = 0,
    Number = 1,
    Boolean = 2,
    Json = 3,
    Null = 4
}

[SugarTable("settings")]
public class SettingTable
{
    [SugarColumn(IsPrimaryKey = true)]
    public string Key { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text")]
    public string? Value { get; set; }

    public SettingType Type { get; set; } = SettingType.String;

    public T? GetValue<T>()
    {
        if (string.IsNullOrEmpty(Value)) return default;

        Type targetType = typeof(T);
        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (Type == SettingType.Json || (!underlyingType.IsPrimitive && underlyingType != typeof(string)))
        {
            return JsonConvert.DeserializeObject<T>(Value);
        }

        if (underlyingType.IsEnum)
        {
            return (T)Enum.Parse(underlyingType, Value);
        }

        return (T)Convert.ChangeType(Value, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
    }


    public void SetValue(object val)
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
            case float:
            case double:
            case decimal:
                Value = Convert.ToString(val, System.Globalization.CultureInfo.InvariantCulture);
                Type = SettingType.Number;
                break;
            case bool b:
                Value = b ? "true" : "false";
                Type = SettingType.Boolean;
                break;
            default:
                Value = JsonConvert.SerializeObject(val);
                Type = SettingType.Json;
                break;
        }
    }

}

public static class SettingKeys
{
    public static class Site
    {
        public const string Prefix = "site.";

        public static class Security
        {
            public const string Prefix = Site.Prefix + "security.";

            public static class Cookie
            {
                public const string Prefix = Security.Prefix + "cookie.";
                public const string Length = Prefix + "length";
                public const string AutoRenew = Prefix + "auto_renew";
                public const string ExpireHours = Prefix + "expire_hours";
            }
        }

        public static class User
        {
            public const string Prefix = Site.Prefix + "user.";
            
            public static class Registration
            {
                public const string Prefix = User.Prefix + "registration.";
                public const string Enabled = Prefix + "enabled";
                public const string ForceInvitation = Prefix + "force_invitation";
            }
        }
    }
}