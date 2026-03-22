using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AINews.Api.Services;

public class RedditService(SettingsService settings, IHttpClientFactory httpFactory, ILogger<RedditService> logger)
{
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public record RedditPost(
        string Id,
        string Title,
        string? Selftext,
        string Author,
        string Permalink,
        long CreatedUtc,
        string? Url);

    public async Task<List<RedditPost>> FetchNewPostsAsync(string subreddit, DateTime? since, int limit = 100)
    {
        var token = await GetAppTokenAsync();
        if (token == null)
        {
            logger.LogWarning("Reddit: no access token available, skipping r/{Subreddit}", subreddit);
            return [];
        }

        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AINews/1.0 by AINewsBot");

        var url = $"https://oauth.reddit.com/r/{Uri.EscapeDataString(subreddit)}/new.json?limit={limit}&raw_json=1";
        var response = await http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Reddit API returned {Status} for r/{Subreddit}", response.StatusCode, subreddit);
            return [];
        }

        // Respect rate limits
        if (response.Headers.TryGetValues("X-Ratelimit-Remaining", out var remaining) &&
            double.TryParse(remaining.FirstOrDefault(), out var rem) && rem < 5)
        {
            if (response.Headers.TryGetValues("X-Ratelimit-Reset", out var reset) &&
                double.TryParse(reset.FirstOrDefault(), out var resetSecs))
            {
                logger.LogWarning("Reddit rate limit low ({Remaining} remaining), reset in {Reset}s", rem, resetSecs);
            }
        }

        var content = await response.Content.ReadAsStringAsync();
        var listing = JsonSerializer.Deserialize<RedditListing>(content, _jsonOptions);

        var posts = listing?.Data?.Children
            ?.Select(c => c.Data)
            .Where(p => p != null)
            .Select(p => new RedditPost(
                p!.Id!,
                p.Title ?? string.Empty,
                p.Selftext,
                p.Author ?? "unknown",
                $"https://reddit.com{p.Permalink}",
                p.CreatedUtc,
                p.Url))
            .ToList() ?? [];

        if (since.HasValue)
        {
            var sinceUnix = new DateTimeOffset(since.Value).ToUnixTimeSeconds();
            posts = posts.Where(p => p.CreatedUtc > sinceUnix).ToList();
        }

        return posts;
    }

    private async Task<string?> GetAppTokenAsync()
    {
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry)
            return _cachedToken;

        var (clientId, clientSecret) = await settings.GetRedditAppCredentialsAsync();
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return null;

        using var http = httpFactory.CreateClient();
        var creds = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AINews/1.0 by AINewsBot");

        var resp = await http.PostAsync("https://www.reddit.com/api/v1/access_token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
            }));

        if (!resp.IsSuccessStatusCode) return null;

        var token = await resp.Content.ReadFromJsonAsync<RedditTokenJson>();
        if (token?.AccessToken == null) return null;

        _cachedToken = token.AccessToken;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60);
        return _cachedToken;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class RedditListing
    {
        public RedditListingData? Data { get; set; }
    }
    private sealed class RedditListingData
    {
        public List<RedditChild>? Children { get; set; }
    }
    private sealed class RedditChild
    {
        public RedditPostData? Data { get; set; }
    }
    private sealed class RedditPostData
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Selftext { get; set; }
        public string? Author { get; set; }
        public string? Permalink { get; set; }
        [JsonPropertyName("created_utc")]
        public long CreatedUtc { get; set; }
        public string? Url { get; set; }
    }
    private sealed record RedditTokenJson(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
