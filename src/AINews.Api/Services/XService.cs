using System.Globalization;
using System.Text.Json;

namespace AINews.Api.Services;

public class XService(SettingsService settings, IHttpClientFactory httpFactory, ILogger<XService> logger)
{
    // Public bearer token used by X.com's own web app — same for all users
    private const string WebBearerToken =
        "AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I4xNbq4=";

    public record Tweet(string Id, string Text, string AuthorId, DateTime CreatedAt);

    public async Task<(List<Tweet> Tweets, string? Error)> SearchTweetsAsync(
        IEnumerable<string> queries, DateTime? since)
    {
        var (authToken, csrfToken) = await settings.GetXCredentialsAsync();
        if (string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(csrfToken))
        {
            logger.LogWarning("X: auth_token or ct0 not configured");
            return ([], "X credentials not configured — add Auth Token and CSRF Token (ct0) in Settings");
        }

        var queryList = queries.ToList();
        if (!queryList.Any()) return ([], null);

        var combined = queryList.Count == 1
            ? queryList[0]
            : string.Join(" OR ", queryList.Select(q => $"({q})"));

        if (since.HasValue)
            combined += $" since:{since.Value:yyyy-MM-dd}";

        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {WebBearerToken}");
        http.DefaultRequestHeaders.TryAddWithoutValidation("x-csrf-token", csrfToken);
        http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"auth_token={authToken}; ct0={csrfToken}");
        http.DefaultRequestHeaders.TryAddWithoutValidation("x-twitter-active-user", "yes");
        http.DefaultRequestHeaders.TryAddWithoutValidation("x-twitter-auth-type", "OAuth2Session");
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

        var url = "https://twitter.com/i/api/2/search/adaptive.json?" +
                  $"q={Uri.EscapeDataString(combined)}&tweet_mode=extended&count=100&include_entities=false";

        var response = await http.GetAsync(url);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            logger.LogWarning("X: 401 — session expired");
            return ([], "X session expired — re-extract auth_token and ct0 from browser cookies");
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("X internal API returned {Status}", response.StatusCode);
            return ([], null);
        }

        var json = await response.Content.ReadAsStringAsync();
        var tweets = ParseAdaptiveResponse(json);
        logger.LogInformation("X search '{Query}': {Count} tweets", combined, tweets.Count);
        return (tweets, null);
    }

    private List<Tweet> ParseAdaptiveResponse(string json)
    {
        var tweets = new List<Tweet>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("globalObjects", out var global)) return tweets;
            if (!global.TryGetProperty("tweets", out var tweetsObj)) return tweets;

            foreach (var prop in tweetsObj.EnumerateObject())
            {
                var t = prop.Value;
                if (t.TryGetProperty("retweeted_status", out _)) continue; // skip retweets

                var id = t.TryGetProperty("id_str", out var p) ? p.GetString() ?? "" : "";
                var text = t.TryGetProperty("full_text", out p) ? p.GetString() ?? "" : "";
                var authorId = t.TryGetProperty("user_id_str", out p) ? p.GetString() ?? "" : "";
                var dateStr = t.TryGetProperty("created_at", out p) ? p.GetString() : null;

                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(text))
                    tweets.Add(new Tweet(id, text, authorId, ParseDate(dateStr)));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse X adaptive search response");
        }
        return tweets;
    }

    private static DateTime ParseDate(string? s)
    {
        // Twitter date format: "Mon Mar 20 12:00:00 +0000 2026"
        if (s != null && DateTime.TryParseExact(s, "ddd MMM dd HH:mm:ss zzz yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.ToUniversalTime();
        return DateTime.UtcNow;
    }
}
