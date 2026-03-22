namespace AINews.Api.DTOs;

public record SourceDto(
    int Id,
    int TopicId,
    string Type,
    string DisplayName,
    string Config,
    bool IsActive,
    DateTime? LastScannedAt,
    DateTime CreatedAt);

public record CreateSourceRequest(int TopicId, string Type, string DisplayName, string Config);
public record UpdateSourceRequest(string DisplayName, string Config, bool IsActive);
