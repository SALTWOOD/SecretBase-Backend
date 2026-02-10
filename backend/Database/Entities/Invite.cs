namespace backend.Database.Entities;

public class Invite
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public int CreatorId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? ExpireAt { get; set; }

    public int MaxUses { get; set; } = 1;

    public int UsedCount { get; set; } = 0;

    public bool IsDisabled { get; set; } = false;

    public User? Creator { get; set; }

    public List<User>? UsedBy { get; set; }

    // Logic Property
    public bool IsValid => !IsDisabled &&
                         UsedCount < MaxUses &&
                         (!ExpireAt.HasValue || ExpireAt > DateTime.Now);
}