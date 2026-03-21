using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;

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

    public static IPostgrestTable<TModel> Page<TModel>(this IPostgrestTable<TModel> query, int page, int pageSize) 
        where TModel : BaseModel, new()
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        int from = (page - 1) * pageSize;
        int to = from + pageSize - 1;

        return query.Range(from, to);
    }
    
    public static Task<TModel?> GetByIdAsync<TModel>(this IPostgrestTable<TModel> table, Guid id) 
        where TModel : BaseModel, new()
    {
        return table.Filter("id", Constants.Operator.Equals, id).Single();
    }
}