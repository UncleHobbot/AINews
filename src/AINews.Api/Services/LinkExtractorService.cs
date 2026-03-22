using System.Text.RegularExpressions;

namespace AINews.Api.Services;

public class LinkExtractorService
{
    private static readonly Regex UrlRegex = new(
        @"https?://[^\s\]\[""<>{}|\\^`]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public record ExtractedUrl(string Url, string LinkType);

    public List<ExtractedUrl> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<ExtractedUrl>();

        foreach (Match match in UrlRegex.Matches(text))
        {
            var raw = match.Value.TrimEnd('.', ',', ')', ';', '\'', '"');
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) continue;
            var url = uri.ToString();
            if (!seen.Add(url)) continue;

            results.Add(new ExtractedUrl(url, ClassifyUrl(uri)));
        }

        return results;
    }

    private static string ClassifyUrl(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant().TrimStart('w', '.');

        if (host == "github.com" || host.EndsWith(".github.com"))
        {
            // Only classify as GitHub if it looks like a repo (not just github.com)
            var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? "GitHub" : "Other";
        }

        if (host == "youtube.com" || host == "youtu.be")
            return "YouTube";

        if (host.Contains("docs.") || uri.AbsolutePath.Contains("/docs/") ||
            uri.AbsolutePath.Contains("/documentation/") || host == "readthedocs.io" ||
            host.EndsWith(".readthedocs.io"))
            return "Docs";

        return "Article";
    }
}
