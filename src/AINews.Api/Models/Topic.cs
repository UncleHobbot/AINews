namespace AINews.Api.Models;

public class Topic
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Source> Sources { get; set; } = new List<Source>();
    public ICollection<NewsItem> NewsItems { get; set; } = new List<NewsItem>();
}
