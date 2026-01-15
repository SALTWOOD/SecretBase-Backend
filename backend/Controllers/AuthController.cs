using backend.Models;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    public class AuthController : BaseApiController
    {
        [HttpPost("login")]
        public IActionResult Login([FromBody] AuthLoginModel model)
        {
            if (model.Email == "test@test.com" && BCrypt.Net.BCrypt.HashPassword(model.Password) == "<I WILL COMPLETE READ HASH CODES LATER>")
            {
                // Issue Token Here
                return Ok(new
                {
                    Token
                });
            }
            return Unauthorized();
        }
    }
}
