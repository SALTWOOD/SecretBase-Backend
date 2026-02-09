using SqlSugar;

namespace backend.OAuth;

[SugarTable("openiddict_applications")]
public class OpenIddictSqlSugarApplication
{
    [SugarColumn(IsPrimaryKey = true, ColumnDescription = "Unique identifier")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [SugarColumn(IsNullable = false, Length = 100)]
    public string? ClientId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ClientSecret { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DisplayName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ClientType { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? PostLogoutRedirectUris { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? Requirements { get; set; }

    [SugarColumn(IsNullable = false)]
    public string ApplicationType { get; set; } = "confidential";

    [SugarColumn(IsNullable = false)]
    public string ConsentType { get; set; } = "explicit";

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? Properties { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? Permissions { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? RedirectUris { get; set; }
}