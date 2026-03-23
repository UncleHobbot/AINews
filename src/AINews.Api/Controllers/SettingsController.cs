using AINews.Api.DTOs;
using AINews.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AINews.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(SettingsService settings) : ControllerBase
{
    private static readonly string[] PublicKeys =
    [
        SettingsService.Keys.XAuthToken,
        SettingsService.Keys.XCsrfToken,
        SettingsService.Keys.ZAiApiKey,
        SettingsService.Keys.ZAiBaseUrl,
        SettingsService.Keys.OpenAiApiKey,
    ];

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var status = await settings.GetAllStatusAsync();
        var result = PublicKeys.Select(k => new SettingDto(k, null, status.GetValueOrDefault(k)));
        return Ok(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest req)
    {
        foreach (var (key, value) in req.Settings)
        {
            if (!PublicKeys.Contains(key)) continue;
            if (!string.IsNullOrWhiteSpace(value))
                await settings.SetAsync(key, value);
        }
        return Ok();
    }
}
