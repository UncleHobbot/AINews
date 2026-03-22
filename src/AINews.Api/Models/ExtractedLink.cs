namespace AINews.Api.Models;

public class ExtractedLink
{
    public int Id { get; set; }
    public int NewsItemId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string LinkType { get; set; } = "Other"; // "GitHub" | "YouTube" | "Article" | "Docs" | "Other"
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public DateTime? FetchedAt { get; set; }
    public string FetchStatus { get; set; } = "Pending"; // "Pending" | "Fetched" | "Failed" | "Skipped"

    public NewsItem NewsItem { get; set; } = null!;
}
