namespace AINews.Api.DTOs;

public record TopicDto(int Id, string Name, string? Description, DateTime CreatedAt, int SourceCount);
public record CreateTopicRequest(string Name, string? Description);
public record UpdateTopicRequest(string Name, string? Description);
