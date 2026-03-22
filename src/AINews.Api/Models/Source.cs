namespace AINews.Api.Models;

public class Source
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public string Type { get; set; } = string.Empty; // "Reddit" | "X"
    public string DisplayName { get; set; } = string.Empty;
    public string Config { get; set; } = "{}"; // JSON blob
    public bool IsActive { get; set; } = true;
    public DateTime? LastScannedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Topic Topic { get; set; } = null!;
    public ICollection<RawPost> RawPosts { get; set; } = new List<RawPost>();
    public ICollection<NewsItem> NewsItems { get; set; } = new List<NewsItem>();
}
