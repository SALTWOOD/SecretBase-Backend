using backend.Services;
using backend.Types.Requests;
using backend.Types.Responses;
using backend.Types.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace backend.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("admin/shortcodes")]
[Produces(MediaTypeNames.Application.Json)]
public class AdminShortcodeController : BaseApiController
{
    private readonly ShortcodeService _shortcodeService;
    private readonly ILogger<AdminShortcodeController> _logger;

    public AdminShortcodeController(
        BaseServices deps,
        ShortcodeService shortcodeService,
        ILogger<AdminShortcodeController> logger) : base(deps)
    {
        _shortcodeService = shortcodeService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有简码列表
    /// </summary>
    [HttpGet]
    [ProducesResponseType<List<ShortcodeDetail>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllShortcodes()
    {
        var shortcodes = await _shortcodeService.GetAllShortcodesAsync();
        return Ok(shortcodes);
    }

    /// <summary>
    /// 获取简码详情
    /// </summary>
    /// <param name="id">简码 ID</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ShortcodeDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetShortcode(int id)
    {
        var shortcode = await _shortcodeService.GetShortcodeByIdAsync(id);

        if (shortcode == null)
        {
            return NotFound(new MessageResponse { Message = $"Shortcode with ID {id} not found" });
        }

        return Ok(shortcode);
    }

    /// <summary>
    /// 创建简码
    /// </summary>
    /// <param name="model">创建请求</param>
    [HttpPost]
    [ProducesResponseType<ShortcodeDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateShortcode([FromBody] ShortcodeCreateModel model)
    {
        var user = await CurrentUser;
        if (user == null)
        {
            return Unauthorized(new MessageResponse { Message = "Current user not found" });
        }

        try
        {
            var shortcode = await _shortcodeService.CreateShortcodeAsync(model, user.Id);
            return CreatedAtAction(
                nameof(GetShortcode),
                new { id = shortcode.Id },
                shortcode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create shortcode");
            return BadRequest(new MessageResponse { Message = "Failed to create shortcode" });
        }
    }

    /// <summary>
    /// 更新简码
    /// </summary>
    /// <param name="id">简码 ID</param>
    /// <param name="model">更新请求</param>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ShortcodeDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateShortcode(int id, [FromBody] ShortcodeUpdateModel model)
    {
        var shortcode = await _shortcodeService.UpdateShortcodeAsync(id, model);

        if (shortcode == null)
        {
            return NotFound(new MessageResponse { Message = $"Shortcode with ID {id} not found" });
        }

        return Ok(shortcode);
    }

    /// <summary>
    /// 更新简码状态
    /// </summary>
    /// <param name="id">简码 ID</param>
    /// <param name="model">状态更新请求</param>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<ShortcodeDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateShortcodeStatus(int id, [FromBody] ShortcodeStatusModel model)
    {
        var shortcode = await _shortcodeService.UpdateShortcodeStatusAsync(id, model.IsEnabled);

        if (shortcode == null)
        {
            return NotFound(new MessageResponse { Message = $"Shortcode with ID {id} not found" });
        }

        return Ok(shortcode);
    }

    /// <summary>
    /// 删除简码
    /// </summary>
    /// <param name="id">简码 ID</param>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteShortcode(int id)
    {
        var deleted = await _shortcodeService.DeleteShortcodeAsync(id);

        if (!deleted)
        {
            return NotFound(new MessageResponse { Message = $"Shortcode with ID {id} not found" });
        }

        return NoContent();
    }
}
