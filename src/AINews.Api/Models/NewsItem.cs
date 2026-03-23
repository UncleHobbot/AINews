namespace AINews.Api.Models;

public class NewsItem
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public int TopicId { get; set; }
    public int RawPostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AiSummary { get; set; }
    public string? AiInsights { get; set; } // JSON array of strings
    public double Relevance { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UserFeedback { get; set; } // null | "Liked" | "Disliked"
    public DateTime? FeedbackAt { get; set; }

    public Source Source { get; set; } = null!;
    public Topic Topic { get; set; } = null!;
    public RawPost RawPost { get; set; } = null!;
    public ICollection<ExtractedLink> ExtractedLinks { get; set; } = new List<ExtractedLink>();
}
