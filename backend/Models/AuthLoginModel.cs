using System.Text.Json.Serialization;

namespace backend.Models
{
    public class AuthLoginModel
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
        public string? CaptchaToken { get; set; }
    }
}
