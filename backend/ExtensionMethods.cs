using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace backend;

public static class ExtensionMethods
{
    public static T ThrowIfNull<T>(
        this T? value,
        string message = "Value cannot be null",
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName, message);
        return value;
    }

    public static T ThrowIfNull<T>(
        this T? value,
        string message = "Value cannot be null",
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : struct
    {
        if (!value.HasValue)
            throw new ArgumentNullException(paramName, message);
        return value.Value;
    }

    public static async Task<(List<T> Items, int TotalCount)> ToPageListAsync<T>(
        this IQueryable<T> query,
        int page,
        int size)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, totalCount);
    }
}