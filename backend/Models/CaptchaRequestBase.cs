namespace backend.Models;

public interface ICaptchaRequest
{
    public string? CaptchaToken { get; set; }
}

public abstract class CaptchaRequestBase : ICaptchaRequest
{
    public string? CaptchaToken { get; set; }
}