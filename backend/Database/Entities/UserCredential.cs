namespace backend.Database.Entities;

public class UserCredential
{
    public int Id { get; set; }

    public byte[] CredentialId { get; set; } = Array.Empty<byte>();

    public byte[] PublicKey { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; }

    public uint SignatureCounter { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public int UserId { get; set; }
}