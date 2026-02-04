namespace backend.Types.Request;

public class AuthLoginModel : CaptchaRequestBase
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}
