using backend.Database.Entities;
using backend.Services;
using backend.Types.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mime;
using System.Text.Json;
using backend.Services.Shortcodes;
using backend.Types.Response;

namespace backend.Controllers;

/// <summary>
/// 简码公开 API 控制器
/// </summary>
[ApiController]
[Route("shortcodes")]
[Produces(MediaTypeNames.Application.Json)]
public class ShortcodeController : BaseApiController
{
    private readonly ShortcodeService _shortcodeService;
    private readonly ILogger<ShortcodeController> _logger;

    public ShortcodeController(
        BaseServices deps,
        ShortcodeService shortcodeService,
        ILogger<ShortcodeController> logger) : base(deps)
    {
        _shortcodeService = shortcodeService;
        _logger = logger;
    }

    /// <summary>
    /// 获取简码列表
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ShortcodeListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShortcodes()
    {
        var shortcodes = await _shortcodeService.GetPublicShortcodesAsync();
        return Ok(new ShortcodeListResponse { Shortcodes = shortcodes });
    }

    /// <summary>
    /// 获取前端代码
    /// </summary>
    /// <param name="name">简码名称</param>
    [HttpGet("{name}/frontend")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, MediaTypeNames.Text.JavaScript)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFrontendCode(string name)
    {
        var code = await _shortcodeService.GetFrontendCodeAsync(name);

        if (code == null)
            return NotFound(new MessageResponse { Message = $"Shortcode '{name}' not found or is disabled" });

        return Content(code, MediaTypeNames.Text.JavaScript);
    }

    /// <summary>
    /// 调用后端 Handler
    /// </summary>
    /// <param name="name">简码名称</param>
    /// <param name="handlerName">Handler 函数名</param>
    /// <param name="requestBody">请求体</param>
    [HttpPost("{name}/handlers/{handlerName}")]
    [ProducesResponseType<ShortcodeExecutionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ShortcodeExecutionResult>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ShortcodeExecutionResult>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ShortcodeExecutionResult>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ShortcodeExecutionResult>(StatusCodes.Status500InternalServerError)]
    [AllowAnonymous]
    public async Task<IActionResult> ExecuteHandler(
        string name,
        string handlerName,
        [FromBody] JsonElement requestBody)
    {
        var headers = Request.Headers
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        var query = Request.Query
            .ToDictionary(q => q.Key, q => q.Value.ToString());

        var currentUser = await CurrentUser;

        var result = await _shortcodeService.ExecuteHandlerAsync(
            name,
            handlerName,
            requestBody,
            headers,
            query,
            currentUser);

        if (!result.Success)
        {
            var statusCode = result.Error?.Code switch
            {
                "SHORTCODE_NOT_FOUND" => StatusCodes.Status404NotFound,
                "HANDLER_NOT_FOUND" => StatusCodes.Status404NotFound,
                "SHORTCODE_DISABLED" => StatusCodes.Status403Forbidden,
                "UNAUTHORIZED" => StatusCodes.Status401Unauthorized,
                "FORBIDDEN" => StatusCodes.Status403Forbidden,
                "TIMEOUT_ERROR" => StatusCodes.Status504GatewayTimeout,
                _ => StatusCodes.Status500InternalServerError
            };

            return StatusCode(statusCode, result);
        }

        return Ok(result);
    }
}