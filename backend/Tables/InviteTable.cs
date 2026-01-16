using SqlSugar;

namespace backend.Tables;


[SugarTable("invites")]
public class InviteTable
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IndexGroupNameList = new string[] { "idx_code" })]
    public string Code { get; set; } = string.Empty;

    public int IssuedBy { get; set; }

    public DateTime TimeIssued { get; set; } = DateTime.Now;

    public int RemainingUses { get; set; } = 1;

    [Navigate(NavigateType.OneToOne, nameof(IssuedBy))]
    public UserTable? Issuer { get; set; }
}