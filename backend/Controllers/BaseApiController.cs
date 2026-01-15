using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("/api/v1/[controller]")]
    public class BaseApiController : ControllerBase
    {
        protected int CurrentUserId =>
            int.Parse(User.FindFirst("id")?.Value
            ?? throw new UnauthorizedAccessException("Invalid user identity."));
    }
}