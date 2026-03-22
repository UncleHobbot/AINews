using AINews.Api.DTOs;
using AINews.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AINews.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController(SettingsService settings) : ControllerBase
{
    private static readonly string[] PublicKeys =
    [
        SettingsService.Keys.RedditClientId,
        SettingsService.Keys.RedditClientSecret,
        SettingsService.Keys.XBearerToken,
        SettingsService.Keys.ZAiApiKey,
        SettingsService.Keys.ZAiBaseUrl,
        SettingsService.Keys.OpenAiApiKey,
        SettingsService.Keys.GoogleClientId,
        SettingsService.Keys.GoogleClientSecret,
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

    [HttpGet("reddit/auth-url")]
    public async Task<IActionResult> RedditAuthUrl()
    {
        var (clientId, _) = await settings.GetRedditAppCredentialsAsync();
        if (string.IsNullOrEmpty(clientId))
            return BadRequest("Reddit Client ID not configured.");

        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/settings/reddit/callback";
        var state = Guid.NewGuid().ToString("N");
        var authUrl = $"https://www.reddit.com/api/v1/authorize?client_id={Uri.EscapeDataString(clientId)}" +
                      $"&response_type=code&state={state}&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&duration=permanent&scope=read";
        return Ok(new RedditAuthUrlResponse(authUrl));
    }

    [HttpGet("reddit/callback")]
    public async Task<IActionResult> RedditCallback([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrEmpty(code)) return Redirect("/?error=reddit_auth_failed");

        var (clientId, clientSecret) = await settings.GetRedditAppCredentialsAsync();
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return Redirect("/?error=reddit_not_configured");

        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/settings/reddit/callback";

        using var http = new HttpClient();
        var creds = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AINews/1.0");

        var resp = await http.PostAsync("https://www.reddit.com/api/v1/access_token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
            }));

        if (!resp.IsSuccessStatusCode) return Redirect("/?error=reddit_token_failed");

        var json = await resp.Content.ReadFromJsonAsync<RedditTokenResponse>();
        if (json?.AccessToken == null) return Redirect("/?error=reddit_token_parse_failed");

        await settings.SetAsync(SettingsService.Keys.RedditAccessToken, json.AccessToken);
        if (json.RefreshToken != null)
            await settings.SetAsync(SettingsService.Keys.RedditRefreshToken, json.RefreshToken);
        var expiresAt = DateTime.UtcNow.AddSeconds(json.ExpiresIn - 60);
        await settings.SetAsync(SettingsService.Keys.RedditTokenExpiresAt, expiresAt.ToString("O"));

        return Redirect("/settings?reddit=connected");
    }

    private sealed record RedditTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string? AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn);
}
