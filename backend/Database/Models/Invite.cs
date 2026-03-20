using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace backend.Database.Models;

[Table("invites")]
public class Invite : BaseModel
{
    [PrimaryKey("id", true)]
    public Guid Id { get; set; }

    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("creator_id")]
    public Guid CreatorId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("expire_at")]
    public DateTime? ExpireAt { get; set; }

    [Column("max_uses")]
    public int MaxUses { get; set; } = 1;

    [Column("used_count")]
    public int UsedCount { get; set; } = 0;

    [Column("is_disabled")]
    public bool IsDisabled { get; set; } = false;

    [Reference(typeof(Profile), foreignKey: "creator_id")]
    public Profile? Creator { get; set; }

    public bool IsValid => !IsDisabled &&
                           UsedCount < MaxUses &&
                           (!ExpireAt.HasValue || ExpireAt > DateTime.UtcNow);
}