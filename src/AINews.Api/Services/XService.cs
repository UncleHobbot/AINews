using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AINews.Api.Services;

public class XService(SettingsService settings, IHttpClientFactory httpFactory, ILogger<XService> logger)
{
    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(15);

    public record Tweet(string Id, string Text, string AuthorId, DateTime CreatedAt);

    public async Task<(List<Tweet> Tweets, string? CooldownRemaining)> SearchTweetsAsync(
        IEnumerable<string> queries, DateTime? since)
    {
        // Rate limit gate
        var lastSearchStr = await settings.GetAsync(SettingsService.Keys.XLastSearchAt);
        if (lastSearchStr != null && DateTime.TryParse(lastSearchStr, out var lastSearch))
        {
            var elapsed = DateTime.UtcNow - lastSearch;
            if (elapsed < CooldownPeriod)
            {
                var remaining = CooldownPeriod - elapsed;
                logger.LogWarning("X API cooldown: {Remaining:mm\\:ss} remaining", remaining);
                return ([], $"{(int)remaining.TotalMinutes}m {remaining.Seconds}s");
            }
        }

        var bearerToken = await settings.GetXBearerTokenAsync();
        if (string.IsNullOrEmpty(bearerToken))
        {
            logger.LogWarning("X: no bearer token configured");
            return ([], null);
        }

        // Batch all queries into one OR compound query
        var queryList = queries.ToList();
        if (!queryList.Any()) return ([], null);
        var combinedQuery = queryList.Count == 1
            ? queryList[0]
            : string.Join(" OR ", queryList.Select(q => $"({q})"));

        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var urlBuilder = new UriBuilder("https://api.twitter.com/2/tweets/search/recent");
        var queryParams = new Dictionary<string, string>
        {
            ["query"] = combinedQuery,
            ["max_results"] = "100",
            ["tweet.fields"] = "created_at,author_id,text",
        };
        if (since.HasValue)
            queryParams["start_time"] = since.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");

        urlBuilder.Query = string.Join("&",
            queryParams.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var response = await http.GetAsync(urlBuilder.Uri);

        // Record the search time regardless of success (to respect rate limits)
        await settings.SetAsync(SettingsService.Keys.XLastSearchAt, DateTime.UtcNow.ToString("O"));

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning("X API 429 Too Many Requests");
            return ([], "15m 0s");
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("X API returned {Status}", response.StatusCode);
            return ([], null);
        }

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<XSearchResponse>(content, _jsonOptions);

        var tweets = result?.Data?
            .Select(t => new Tweet(
                t.Id,
                t.Text,
                t.AuthorId ?? string.Empty,
                t.CreatedAt ?? DateTime.UtcNow))
            .ToList() ?? [];

        return (tweets, null);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class XSearchResponse
    {
        public List<XTweetData>? Data { get; set; }
    }
    private sealed class XTweetData
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        [JsonPropertyName("author_id")]
        public string? AuthorId { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}
