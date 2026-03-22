using System.Text.Json;
using HtmlAgilityPack;

namespace AINews.Api.Services;

public class ContentFetcherService(IHttpClientFactory httpFactory, ILogger<ContentFetcherService> logger)
{
    private const int MaxContentLength = 4000;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public async Task<(string? Title, string? Content)> FetchAsync(string url, string linkType)
    {
        try
        {
            return linkType switch
            {
                "GitHub" => await FetchGitHubAsync(url),
                "YouTube" => await FetchYouTubeAsync(url),
                _ => await FetchArticleAsync(url),
            };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to fetch content for {Url}", url);
            return (null, null);
        }
    }

    private async Task<(string?, string?)> FetchGitHubAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return (null, null);
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length < 2) return (null, null);

        var owner = parts[0];
        var repo = parts[1];
        var rawUrl = $"https://raw.githubusercontent.com/{owner}/{repo}/HEAD/README.md";

        using var http = CreateClient();
        var content = await http.GetStringAsync(rawUrl);
        var truncated = content.Length > MaxContentLength ? content[..MaxContentLength] : content;
        return ($"{owner}/{repo}", truncated);
    }

    private async Task<(string?, string?)> FetchYouTubeAsync(string url)
    {
        using var http = CreateClient();
        var oEmbedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(url)}&format=json";
        var json = await http.GetStringAsync(oEmbedUrl);
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        var title = doc.TryGetProperty("title", out var t) ? t.GetString() : null;
        var author = doc.TryGetProperty("author_name", out var a) ? a.GetString() : null;
        return (title, author != null ? $"YouTube video by {author}" : null);
    }

    private async Task<(string?, string?)> FetchArticleAsync(string url)
    {
        using var http = CreateClient();
        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return (null, null);

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (!contentType.Contains("html")) return (null, null);

        var html = await response.Content.ReadAsStringAsync();
        if (html.Length > 500_000) html = html[..500_000];

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Extract title
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        var title = titleNode?.InnerText?.Trim();

        // Remove scripts, styles, nav, footer
        var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//nav|//footer|//header");
        foreach (var node in nodesToRemove ?? Enumerable.Empty<HtmlNode>())
            node.Remove();

        // Try to find main content
        var mainNode = doc.DocumentNode.SelectSingleNode("//main|//article|//div[@id='content']|//div[@class='content']")
            ?? doc.DocumentNode.SelectSingleNode("//body")
            ?? doc.DocumentNode;

        var text = HtmlEntity.DeEntitize(mainNode.InnerText);
        // Collapse whitespace
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length > MaxContentLength) text = text[..MaxContentLength];

        return (title, text);
    }

    private HttpClient CreateClient()
    {
        var http = httpFactory.CreateClient();
        http.Timeout = RequestTimeout;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 AINews/1.0");
        return http;
    }
}
