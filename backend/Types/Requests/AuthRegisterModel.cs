namespace backend.Types.Request;

public class AuthRegisterModel : CaptchaRequestBase
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public string? InviteCode { get; set; }
}