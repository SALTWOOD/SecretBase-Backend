namespace backend.Database.Models;

using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("profiles")]
public class Profile : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("role")]
    public int Role { get; set; }

    [Column("is_banned")]
    public bool IsBanned { get; set; }

    [Column("avatar")]
    public string? Avatar { get; set; }
    
    [Column("used_invite")]
    public Guid UsedInvite { get; set; }
    
    [Column("username")]
    public string Username { get; set; }
}