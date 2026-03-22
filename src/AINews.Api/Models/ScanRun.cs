namespace AINews.Api.Models;

public class ScanRun
{
    public int Id { get; set; }
    public string TriggeredBy { get; set; } = "Manual";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Running"; // "Running" | "Completed" | "Failed"
    public string? ErrorMessage { get; set; }
    public int TotalSourcesScanned { get; set; }
    public int TotalRawPostsFetched { get; set; }
    public int TotalNewsItemsCreated { get; set; }

    public ICollection<RawPost> RawPosts { get; set; } = new List<RawPost>();
}
