namespace AINews.Api.Models;

public class RawPost
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public int ScanRunId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Author { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime FetchedAt { get; set; }
    public bool IsProcessed { get; set; } = false;

    public Source Source { get; set; } = null!;
    public ScanRun ScanRun { get; set; } = null!;
    public NewsItem? NewsItem { get; set; }
}
