using System.Text.Json;
using System.Text.Json.Serialization;

namespace AINews.Api.Services;

public class RedditService(IHttpClientFactory httpFactory, ILogger<RedditService> logger)
{
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
        using var http = httpFactory.CreateClient();
        // Reddit requires a non-empty User-Agent for unauthenticated requests
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AINews/1.0 (personal aggregator)");

        var url = $"https://www.reddit.com/r/{Uri.EscapeDataString(subreddit)}/new.json?limit={limit}&raw_json=1";
        var response = await http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Reddit returned {Status} for r/{Subreddit}", response.StatusCode, subreddit);
            return [];
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

        logger.LogInformation("Reddit r/{Subreddit}: {Count} new posts", subreddit, posts.Count);
        return posts;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class RedditListing { public RedditListingData? Data { get; set; } }
    private sealed class RedditListingData { public List<RedditChild>? Children { get; set; } }
    private sealed class RedditChild { public RedditPostData? Data { get; set; } }
    private sealed class RedditPostData
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Selftext { get; set; }
        public string? Author { get; set; }
        public string? Permalink { get; set; }
        [JsonPropertyName("created_utc")] public long CreatedUtc { get; set; }
        public string? Url { get; set; }
    }
}
