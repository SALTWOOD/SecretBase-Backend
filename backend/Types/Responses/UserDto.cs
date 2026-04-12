using backend.Database.Entities;

namespace backend.Types.Responses;

public class UserDto
{
    public int? Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Role { get; set; }
    public string Avatar { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? Email { get; set; }
    public bool IsGuest { get; set; }

    public static UserDto FromUser(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Role = (int)user.Role,
            Avatar = user.Avatar,
            Website = user.Website,
            Email = null,
            IsGuest = false
        };
    }

    public static UserDto FromUserWithEmail(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Role = (int)user.Role,
            Avatar = user.Avatar,
            Website = user.Website,
            Email = user.Email,
            IsGuest = false
        };
    }

    public static UserDto FromGuest(CommentMetadata metadata)
    {
        return new UserDto
        {
            Id = null,
            Username = metadata.GuestNickname!,
            Role = 0,
            Avatar = "",
            Website = metadata.GuestWebsite,
            Email = null,
            IsGuest = true
        };
    }

    public static UserDto FromGuestWithEmail(CommentMetadata metadata)
    {
        return new UserDto
        {
            Id = null,
            Username = metadata.GuestNickname!,
            Role = 0,
            Avatar = "",
            Website = metadata.GuestWebsite,
            Email = metadata.GuestEmail,
            IsGuest = true
        };
    }
}
