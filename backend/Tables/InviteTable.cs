using backend.Tables;
using SqlSugar;
using System.Text.Json.Serialization;

[SugarTable("invites")]
[SugarIndex("unique_invites_code", nameof(Code), OrderByType.Desc, true)]
public class InviteTable
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(ColumnDescription = "Unique invite code")]
    public string Code { get; set; } = string.Empty;

    public int CreatorId { get; set; }

    [SugarColumn(ColumnDataType = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [SugarColumn(ColumnDataType = "timestamptz", IsNullable = true)]
    public DateTime? ExpireAt { get; set; }

    public int MaxUses { get; set; } = 1;

    public int UsedCount { get; set; } = 0;

    public bool IsDisabled { get; set; } = false;

    [Navigate(NavigateType.OneToOne, nameof(CreatorId))]
    public UserTable? Creator { get; set; }

    // Logic Property
    [JsonIgnore]
    [SugarColumn(IsIgnore = true)]
    public bool IsValid => !IsDisabled &&
                         UsedCount < MaxUses &&
                         (!ExpireAt.HasValue || ExpireAt > DateTime.Now);
}