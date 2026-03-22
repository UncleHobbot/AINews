using AINews.Api.Data;
using AINews.Api.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace AINews.Api.Services;

public class SettingsService(AppDbContext db, IDataProtectionProvider dataProtection)
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("AINews.Settings");

    public static class Keys
    {
        public const string RedditClientId = "Reddit:ClientId";
        public const string RedditClientSecret = "Reddit:ClientSecret";
        public const string RedditAccessToken = "Reddit:AccessToken";
        public const string RedditRefreshToken = "Reddit:RefreshToken";
        public const string RedditTokenExpiresAt = "Reddit:TokenExpiresAt";
        public const string XBearerToken = "X:BearerToken";
        public const string XLastSearchAt = "X:LastSearchAt";
        public const string ZAiApiKey = "ZAi:ApiKey";
        public const string ZAiBaseUrl = "ZAi:BaseUrl";
        public const string OpenAiApiKey = "OpenAi:ApiKey";
        public const string GoogleClientId = "Google:ClientId";
        public const string GoogleClientSecret = "Google:ClientSecret";
    }

    public async Task<string?> GetAsync(string key)
    {
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting?.Value == null) return null;
        try { return _protector.Unprotect(setting.Value); }
        catch { return null; }
    }

    public async Task SetAsync(string key, string value)
    {
        var encrypted = _protector.Protect(value);
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = encrypted, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            setting.Value = encrypted;
            setting.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    public async Task<bool> IsSetAsync(string key)
        => await db.AppSettings.AnyAsync(s => s.Key == key && s.Value != null);

    public async Task<Dictionary<string, bool>> GetAllStatusAsync()
    {
        var allKeys = new[]
        {
            Keys.RedditClientId, Keys.RedditClientSecret, Keys.RedditAccessToken,
            Keys.XBearerToken, Keys.ZAiApiKey, Keys.ZAiBaseUrl, Keys.OpenAiApiKey,
            Keys.GoogleClientId, Keys.GoogleClientSecret
        };
        var existing = await db.AppSettings
            .Where(s => allKeys.Contains(s.Key) && s.Value != null)
            .Select(s => s.Key)
            .ToListAsync();
        return allKeys.ToDictionary(k => k, k => existing.Contains(k));
    }

    // Typed accessors
    public async Task<(string? clientId, string? clientSecret)> GetRedditAppCredentialsAsync()
        => (await GetAsync(Keys.RedditClientId), await GetAsync(Keys.RedditClientSecret));

    public async Task<string?> GetXBearerTokenAsync() => await GetAsync(Keys.XBearerToken);

    public async Task<(string? apiKey, string? baseUrl)> GetZAiCredentialsAsync()
        => (await GetAsync(Keys.ZAiApiKey), await GetAsync(Keys.ZAiBaseUrl));

    public async Task<string?> GetOpenAiApiKeyAsync() => await GetAsync(Keys.OpenAiApiKey);
}
