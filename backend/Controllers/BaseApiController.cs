using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("/api/v1/[controller]")]
    public class BaseApiController : ControllerBase { }
}