using SqlSugar;

namespace backend.OAuth;

[SugarTable("openiddict_authorizations")]
public class OpenIddictSqlSugarAuthorization
{
    [SugarColumn(IsPrimaryKey = true)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [SugarColumn(IsNullable = true)]
    public string? ApplicationId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Subject { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Type { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? Scopes { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Status { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? CreationDate { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? Properties { get; set; }
}