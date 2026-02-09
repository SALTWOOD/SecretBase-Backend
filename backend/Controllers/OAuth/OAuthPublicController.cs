namespace backend.Controllers.OAuth;

using backend.Types.Response;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

[ApiController]
[Route("oauth/public")]
public class OAuthPublicController : ControllerBase
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ILogger<OAuthPublicController> _logger;

    public OAuthPublicController(IOpenIddictApplicationManager applicationManager, ILogger<OAuthPublicController> logger)
    {
        _applicationManager = applicationManager;
        _logger = logger;
    }

    [HttpGet("app-info")]
    [ProducesResponseType<OAuthAppResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppInfo(string clientId)
    {
        // 1. 使用 Manager 查询应用实体
        // 注意：这里使用的是你之前定义的 OpenIddictSqlSugarApplication 实体
        var application = await _applicationManager.FindByClientIdAsync(clientId);

        if (application == null)
        {
            _logger.LogWarning("Public info request for non-existent client: {ClientId}", clientId);
            return NotFound(new MessageResponse { Message = "Application not found" });
        }

        // 2. 映射为 DTO 返回给前端
        // 只暴露安全的公开字段，绝对不要暴露 ClientSecret 喵！
        var response = new OAuthAppResponse
        {
            ClientId = (await _applicationManager.GetClientIdAsync(application)).ThrowIfNull(),
            DisplayName = (await _applicationManager.GetDisplayNameAsync(application)).ThrowIfNull(),
            // 如果你扩展了图标字段，也可以在这里返回
        };

        return Ok(response);
    }
}