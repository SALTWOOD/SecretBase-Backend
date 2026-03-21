using System.Text.Json;

namespace backend.Database.Entities;

public enum SettingType
{
    String = 0,
    Number = 1,
    Boolean = 2,
    Json = 3,
    Null = 4
}

public class Setting
{
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public SettingType Type { get; set; } = SettingType.String;

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
                Value = JsonSerializer.Serialize(val);
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

        public static class Seo
        {
            public const string Prefix = Site.Prefix + "seo.";
            public const string Title = Prefix + "title";
            public const string Description = Prefix + "description";
            public const string Keywords = Prefix + "keywords";
            public const string OgTitle = Prefix + "og_title";
            public const string OgDescription = Prefix + "og_description";
            public const string OgImage = Prefix + "og_image";
            public const string TwitterCard = Prefix + "twitter_card";
            public const string Robots = Prefix + "robots";
        }

        public static class Security
        {
            public const string Prefix = Site.Prefix + "security.";

            public static class Cookie
            {
                public const string Prefix = Security.Prefix + "cookie.";
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

        public static class Home
        {
            public const string Prefix = Site.Prefix + "home.";

            public static class Background
            {
                public const string Prefix = Home.Prefix + "background.";
                public const string Url = Prefix + "url";
                public const string Blur = Prefix + "blur"; // 虚化度
                public const string Opacity = Prefix + "opacity"; // 透明度
            }

            public static class Banner
            {
                public const string Prefix = Home.Prefix + "banner.";
                public const string Content = Prefix + "content"; // 文字内容

                /// <summary>
                /// Display mode: full | mini | screen | hidden
                /// </summary>
                public const string DisplayMode = Prefix + "display_mode";
            }
        }
    }
}