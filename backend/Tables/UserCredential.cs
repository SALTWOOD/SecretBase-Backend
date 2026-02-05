using SqlSugar;

namespace backend.Tables;

[SugarTable("user_credentials")]
public class UserCredential
{
    [SugarColumn(IsPrimaryKey = true)]
    public string Id { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "bytea")]
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();

    public uint SignatureCounter { get; set; }

    public int UserId { get; set; }
}