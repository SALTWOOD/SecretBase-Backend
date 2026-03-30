using System.Text.Json;
using backend.Database;
using backend.Database.Entities;
using backend.Services;
using backend.SourceGenerators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers.Admin;

public readonly record struct SettingListItem
{
    public string Key { get; init; }

    public string Type { get; init; }

    public object? DefaultValue { get; init; }

    public object? CurrentValue { get; init; }
}

public readonly record struct UpdateSettingBody
{
    public JsonElement Value { get; init; }
}

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("admin/settings")]
public class AdminSettingsController(BaseServices deps) : BaseApiController(deps)
{
    [HttpGet]
    [ProducesResponseType(typeof(List<SettingListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SettingListItem>>> GetAllSettings()
    {
        var result = new List<SettingListItem>();

        foreach (var keyWithType in SettingRegistry.Keys)
        {
            var key = SettingRegistry.ExtractKey(keyWithType);
            var type = SettingRegistry.ExtractType(keyWithType);
            var defaultValue = SettingRegistry.DefaultValues.TryGetValue(keyWithType, out var dv) ? dv : null;

            // 获取当前值
            object? currentValue = null;
            if (SettingNode.GlobalProvider != null)
            {
                currentValue = await SettingNode.GlobalProvider.GetAsync<object?>(key);
            }

            result.Add(new SettingListItem
            {
                Key = key,
                Type = type,
                DefaultValue = defaultValue,
                CurrentValue = currentValue
            });
        }

        return Ok(result);
    }

    [HttpGet("{*key}")]
    [ProducesResponseType(typeof(SettingListItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettingListItem>> GetSetting(string key)
    {
        // 查找匹配的 keyWithType
        var keyWithType = SettingRegistry.Keys.FirstOrDefault(k =>
            SettingRegistry.ExtractKey(k) == key);

        if (keyWithType == null)
            return NotFound(new { message = $"Setting '{key}' not found" });

        var type = SettingRegistry.ExtractType(keyWithType);
        var defaultValue = SettingRegistry.DefaultValues.TryGetValue(keyWithType, out var dv) ? dv : null;

        object? currentValue = null;
        if (SettingNode.GlobalProvider != null)
        {
            currentValue = await SettingNode.GlobalProvider.GetAsync<object?>(key);
        }

        return Ok(new SettingListItem
        {
            Key = key,
            Type = type,
            DefaultValue = defaultValue,
            CurrentValue = currentValue
        });
    }
    
    [HttpPut("{*key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingBody body)
    {
        // 查找匹配的 keyWithType
        var keyWithType = SettingRegistry.Keys.FirstOrDefault(k =>
            SettingRegistry.ExtractKey(k) == key);

        if (keyWithType == null)
            return NotFound(new { message = $"Setting '{key}' not found" });

        if (SettingNode.GlobalProvider == null)
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Setting provider not available" });

        var value = body.Value;
        object? result = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value switch 
            {
                _ when value.TryGetInt32(out int i) => i,
                _ when value.TryGetInt64(out long l) => l,
                _ => (object)value.GetDouble()
            },
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object or JsonValueKind.Array => throw new NotImplementedException(), 
            _ => null
        };
        
        await SettingNode.GlobalProvider.SetAsync(key, result);

        return NoContent();
    }

    [HttpDelete("{*key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetSetting(string key)
    {
        // 查找匹配的 keyWithType
        var keyWithType = SettingRegistry.Keys.FirstOrDefault(k =>
            SettingRegistry.ExtractKey(k) == key);

        if (keyWithType == null)
            return NotFound(new { message = $"Setting '{key}' not found" });

        var defaultValue = SettingRegistry.DefaultValues.TryGetValue(keyWithType, out var dv) ? dv : null;

        if (SettingNode.GlobalProvider == null)
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Setting provider not available" });

        await SettingNode.GlobalProvider.SetAsync(key, defaultValue);

        return NoContent();
    }
}
