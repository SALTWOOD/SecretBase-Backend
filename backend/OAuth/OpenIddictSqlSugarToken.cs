using SqlSugar;

namespace backend.OAuth;

[SugarTable("openiddict_tokens")]
[SugarIndex("index_openiddict_tokens_referenceid", nameof(ReferenceId), OrderByType.Desc)]
public class OpenIddictSqlSugarToken
{
    [SugarColumn(IsPrimaryKey = true)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [SugarColumn(IsNullable = true)]
    public string? ApplicationId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? AuthorizationId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Subject { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Type { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Status { get; set; }

    [SugarColumn(IsNullable = true, Length = 100)]
    public string? ReferenceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? CreationDate { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ExpirationDate { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? RedemptionDate { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? Properties { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? Payload { get; set; }
}