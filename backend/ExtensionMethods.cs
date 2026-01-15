namespace backend;

public static class ExtensionMethods
{
    /// <summary>
    /// 如果值为 null 则抛出异常，否则返回非空值
    /// </summary>
    /// <param name="value">要检查的对象</param>
    /// <param name="message">自定义异常信息</param>
    public static T ThrowIfNull<T>(
        [System.Diagnostics.CodeAnalysis.NotNull] this T? value, 
        string message = "Value cannot be null")
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), message);
        }
        return value;
    }
}