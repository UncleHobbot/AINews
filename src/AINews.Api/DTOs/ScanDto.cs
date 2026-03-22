namespace AINews.Api.DTOs;

public record TriggerScanResponse(int ScanRunId);

public record ScanRunDto(
    int Id,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int TotalSourcesScanned,
    int TotalRawPostsFetched,
    int TotalNewsItemsCreated,
    string? ErrorMessage);

public record ScanProgressDto(
    int ScanRunId,
    string Status,
    string CurrentSource,
    int SourcesCompleted,
    int TotalSources,
    int PostsFetched,
    int NewsItemsCreated,
    string? Message);
