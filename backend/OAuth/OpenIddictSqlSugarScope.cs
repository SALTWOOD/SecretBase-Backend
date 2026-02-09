using SqlSugar;

namespace backend.OAuth;

[SugarTable("openiddict_scopes")]
public class OpenIddictSqlSugarScope
{
    [SugarColumn(IsPrimaryKey = true)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [SugarColumn(IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DisplayName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? Resources { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? Properties { get; set; }
}