using System.Text.Json.Serialization;

namespace backend.Database.Entities;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;

    public bool IsBanned { get; set; } = false;

    // PG recommends using UTC time
    public DateTime RegisterTime { get; set; } = DateTime.UtcNow;

    public string Avatar { get; set; } = string.Empty;

    [JsonIgnore]
    public LastLogin? LastLoginInfo { get; set; }

    public int? UsedInviteId { get; set; }

    [JsonIgnore]
    public string? TotpSecret { get; set; }

    [JsonIgnore]
    public string[]? TotpRecoveryCodes { get; set; }

    [JsonIgnore]
    public bool ForceTwoFactor { get; set; }

    [JsonIgnore]
    public List<Invite> MyIssuedInvites { get; set; } = new();
}

public class LastLogin
{
    public DateTime Time { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}