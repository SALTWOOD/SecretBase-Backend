using SqlSugar;

namespace backend.Tables;

[SugarTable("user_credentials")]
[SugarIndex("unique_user_credentials_credentialid", nameof(CredentialId), OrderByType.Desc, true)]
public class UserCredentialTable
{
    [SugarColumn(IsPrimaryKey = true)]
    public int Id { get; set; }

    [SugarColumn(ColumnDataType = "bytea")]
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();

    [SugarColumn(ColumnDataType = "bytea", IsNullable = false)]
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();

    [SugarColumn(ColumnDataType = "timestamptz")]
    public DateTime CreatedAt { get; set; }

    public uint SignatureCounter { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public int UserId { get; set; }
}