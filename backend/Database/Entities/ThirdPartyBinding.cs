namespace backend.Database.Entities;

public class ThirdPartyBinding
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string ProviderUserId { get; set; } = string.Empty;

    public string ProviderUsername { get; set; } = string.Empty;

    public string? ProviderAvatarUrl { get; set; }

    public string? AccessToken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
