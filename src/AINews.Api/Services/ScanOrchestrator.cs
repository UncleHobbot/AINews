using System.Text.Json;
using AINews.Api.Data;
using AINews.Api.DTOs;
using AINews.Api.Hubs;
using AINews.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LinkSummary = AINews.Api.Services.AiService.LinkSummary;

namespace AINews.Api.Services;

public class ScanOrchestrator(
    AppDbContext db,
    RedditService reddit,
    XService xService,
    AiService ai,
    LinkExtractorService linkExtractor,
    ContentFetcherService contentFetcher,
    PreferenceService preferences,
    IHubContext<ScanProgressHub> hub,
    ILogger<ScanOrchestrator> logger)
{
    private string? _preferenceContext;

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var scanRun = new ScanRun { StartedAt = DateTime.UtcNow, Status = "Running" };
        db.ScanRuns.Add(scanRun);
        await db.SaveChangesAsync(ct);

        // Build preference context once per scan from feedback history
        _preferenceContext = await preferences.BuildContextAsync();
        if (_preferenceContext != null)
            logger.LogInformation("Preference context loaded ({Chars} chars)", _preferenceContext.Length);

        try
        {
            var sources = await db.Sources.Where(s => s.IsActive).Include(s => s.Topic).ToListAsync(ct);
            scanRun.TotalSourcesScanned = 0;

            // Group X sources together to batch their queries
            var redditSources = sources.Where(s => s.Type == "Reddit").ToList();
            var xSources = sources.Where(s => s.Type == "X").ToList();

            // Process Reddit sources
            foreach (var source in redditSources)
            {
                if (ct.IsCancellationRequested) break;
                await ProcessRedditSourceAsync(source, scanRun, sources.Count, ct);
                scanRun.TotalSourcesScanned++;
                await db.SaveChangesAsync(ct);
            }

            // Process all X sources in a single batched call
            if (xSources.Any())
            {
                await ProcessXSourcesAsync(xSources, scanRun, sources.Count, ct);
                scanRun.TotalSourcesScanned += xSources.Count;
                await db.SaveChangesAsync(ct);
            }

            scanRun.Status = "Completed";
            scanRun.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scan run {Id} failed", scanRun.Id);
            scanRun.Status = "Failed";
            scanRun.ErrorMessage = ex.Message;
            scanRun.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await EmitProgress(new ScanProgressDto(
            scanRun.Id, scanRun.Status, "Done",
            scanRun.TotalSourcesScanned, scanRun.TotalSourcesScanned,
            scanRun.TotalRawPostsFetched, scanRun.TotalNewsItemsCreated, null));

        return scanRun.Id;
    }

    private async Task ProcessRedditSourceAsync(Source source, ScanRun scanRun, int totalSources, CancellationToken ct)
    {
        await EmitProgress(new ScanProgressDto(
            scanRun.Id, "Running", source.DisplayName,
            scanRun.TotalSourcesScanned, totalSources,
            scanRun.TotalRawPostsFetched, scanRun.TotalNewsItemsCreated,
            $"Fetching {source.DisplayName}…"));

        var config = TryDeserialize<RedditSourceConfig>(source.Config);
        if (config?.Subreddit == null)
        {
            logger.LogWarning("Source {Id} has invalid config", source.Id);
            return;
        }

        var posts = await reddit.FetchNewPostsAsync(config.Subreddit, source.LastScannedAt, config.Limit ?? 100);
        logger.LogInformation("Reddit r/{Sub}: fetched {Count} posts", config.Subreddit, posts.Count);

        foreach (var post in posts)
        {
            if (ct.IsCancellationRequested) break;
            var raw = await UpsertRawPostAsync(source, scanRun, post.Id,
                $"https://reddit.com{post.Permalink.Replace("https://reddit.com", "")}",
                post.Title, post.Selftext, post.Author,
                DateTimeOffset.FromUnixTimeSeconds(post.CreatedUtc).UtcDateTime);

            if (raw == null) continue; // duplicate
            scanRun.TotalRawPostsFetched++;

            var created = await ProcessRawPostAsync(raw, source, ct);
            if (created) scanRun.TotalNewsItemsCreated++;
        }

        source.LastScannedAt = DateTime.UtcNow;
    }

    private async Task ProcessXSourcesAsync(List<Source> sources, ScanRun scanRun, int totalSources, CancellationToken ct)
    {
        await EmitProgress(new ScanProgressDto(
            scanRun.Id, "Running", "X.com search",
            scanRun.TotalSourcesScanned, totalSources,
            scanRun.TotalRawPostsFetched, scanRun.TotalNewsItemsCreated,
            "Searching X.com…"));

        var queries = sources
            .Select(s => TryDeserialize<XSourceConfig>(s.Config)?.Query)
            .Where(q => !string.IsNullOrEmpty(q))
            .Select(q => q!)
            .Distinct();

        var since = sources.Select(s => s.LastScannedAt).Where(d => d.HasValue)
            .Select(d => d!.Value).DefaultIfEmpty(DateTime.UtcNow.AddDays(-1)).Min();

        var (tweets, cooldown) = await xService.SearchTweetsAsync(queries, since);
        if (cooldown != null)
        {
            logger.LogWarning("X cooldown: {Cooldown}", cooldown);
            return;
        }

        // Assign tweets to the best-matching source by keyword match
        foreach (var tweet in tweets)
        {
            if (ct.IsCancellationRequested) break;
            var matchingSource = FindBestXSource(sources, tweet.Text) ?? sources.First();
            var raw = await UpsertRawPostAsync(matchingSource, scanRun, tweet.Id,
                $"https://x.com/i/web/status/{tweet.Id}",
                null, tweet.Text, tweet.AuthorId, tweet.CreatedAt);

            if (raw == null) continue;
            scanRun.TotalRawPostsFetched++;

            var created = await ProcessRawPostAsync(raw, matchingSource, ct);
            if (created) scanRun.TotalNewsItemsCreated++;
        }

        foreach (var s in sources) s.LastScannedAt = DateTime.UtcNow;
    }

    private async Task<RawPost?> UpsertRawPostAsync(
        Source source, ScanRun scanRun, string externalId, string? url,
        string? title, string? body, string? author, DateTime publishedAt)
    {
        var existing = await db.RawPosts.AnyAsync(p => p.SourceId == source.Id && p.ExternalId == externalId);
        if (existing) return null;

        var raw = new RawPost
        {
            SourceId = source.Id,
            ScanRunId = scanRun.Id,
            ExternalId = externalId,
            ExternalUrl = url,
            Title = title,
            Body = body,
            Author = author,
            PublishedAt = publishedAt,
            FetchedAt = DateTime.UtcNow,
        };
        db.RawPosts.Add(raw);
        await db.SaveChangesAsync();
        return raw;
    }

    private async Task<bool> ProcessRawPostAsync(RawPost raw, Source source, CancellationToken ct)
    {
        var text = $"{raw.Title ?? ""} {raw.Body ?? ""}".Trim();
        var links = linkExtractor.Extract(text);

        // Fetch link content in parallel (max 3 concurrent)
        var linkContents = new Dictionary<string, (string? Title, string? Content)>();
        var sem = new SemaphoreSlim(3);
        var tasks = links.Take(5).Select(async link =>
        {
            await sem.WaitAsync(ct);
            try
            {
                var result = await contentFetcher.FetchAsync(link.Url, link.LinkType);
                lock (linkContents) linkContents[link.Url] = result;
            }
            finally { sem.Release(); }
        });
        await Task.WhenAll(tasks);

        // AI analysis — pass preference context so the model learns from feedback history
        var analysis = await ai.AnalyzePostAsync(
            raw.Title ?? text[..Math.Min(text.Length, 200)],
            raw.Body,
            links.Select(l => l.Url),
            _preferenceContext);

        if (analysis == null || !analysis.ShouldInclude)
        {
            raw.IsProcessed = true;
            await db.SaveChangesAsync(ct);
            return false;
        }

        var newsItem = new NewsItem
        {
            SourceId = source.Id,
            TopicId = source.TopicId,
            RawPostId = raw.Id,
            Title = raw.Title ?? analysis.Summary[..Math.Min(analysis.Summary.Length, 200)],
            AiSummary = analysis.Summary,
            AiInsights = JsonSerializer.Serialize(analysis.Insights),
            Relevance = analysis.Relevance,
            PublishedAt = raw.PublishedAt ?? raw.FetchedAt,
            CreatedAt = DateTime.UtcNow,
        };
        db.NewsItems.Add(newsItem);
        await db.SaveChangesAsync(ct);

        // Summarize and persist extracted links
        foreach (var link in links.Take(5))
        {
            var fetched = linkContents.GetValueOrDefault(link.Url);
            LinkSummary? summary = null;
            if (fetched.Content != null)
                summary = await ai.SummarizeLinkAsync(link.Url, fetched.Content);

            db.ExtractedLinks.Add(new ExtractedLink
            {
                NewsItemId = newsItem.Id,
                Url = link.Url,
                LinkType = link.LinkType,
                Title = summary?.Title ?? fetched.Title,
                Summary = summary?.Summary,
                FetchedAt = fetched.Content != null ? DateTime.UtcNow : null,
                FetchStatus = fetched.Content != null ? "Fetched" : "Failed",
            });
        }

        raw.IsProcessed = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static Source? FindBestXSource(List<Source> sources, string tweetText)
    {
        foreach (var source in sources)
        {
            var config = TryDeserialize<XSourceConfig>(source.Config);
            if (config?.Query == null) continue;
            // Simple keyword match
            var keywords = config.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(k => !k.StartsWith('-') && !k.StartsWith("lang:") && !k.StartsWith("is:"));
            if (keywords.Any(kw => tweetText.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                return source;
        }
        return null;
    }

    private async Task EmitProgress(ScanProgressDto dto)
    {
        try { await hub.Clients.Group("scan-watchers").SendAsync("ScanProgress", dto); }
        catch { /* non-critical */ }
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (json == null) return null;
        try { return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch { return null; }
    }

    // Config shapes
    private sealed class RedditSourceConfig
    {
        public string? Subreddit { get; set; }
        public int? Limit { get; set; }
    }
    private sealed class XSourceConfig
    {
        public string? Query { get; set; }
        public int? MaxResults { get; set; }
    }
}

