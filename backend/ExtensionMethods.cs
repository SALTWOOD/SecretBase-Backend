using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace backend;

public static class ExtensionMethods
{
    public static T ThrowIfNull<T>(
        [NotNull] this T? value,
        string message = "Value cannot be null",
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName, message);
        return value;
    }

    public static T ThrowIfNull<T>(
        [NotNull] this T? value,
        string message = "Value cannot be null",
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : struct
    {
        if (!value.HasValue)
            throw new ArgumentNullException(paramName, message);
        return value.Value;
    }
}