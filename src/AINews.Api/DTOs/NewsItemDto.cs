namespace AINews.Api.DTOs;

public record ExtractedLinkDto(
    int Id,
    string Url,
    string LinkType,
    string? Title,
    string? Summary,
    string FetchStatus);

public record NewsItemDto(
    int Id,
    int TopicId,
    string TopicName,
    string SourceType,
    string SourceDisplayName,
    string Title,
    string? AiSummary,
    string[]? AiInsights,
    double Relevance,
    DateTime PublishedAt,
    string? ExternalUrl,
    string? Author,
    IEnumerable<ExtractedLinkDto> ExtractedLinks,
    string? UserFeedback);

public record FeedbackRequest(string? Feedback); // "Liked" | "Disliked" | null
