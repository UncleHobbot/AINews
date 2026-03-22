namespace AINews.Api.DTOs;

public record SettingDto(string Key, string? MaskedValue, bool IsSet);

public record UpdateSettingsRequest(Dictionary<string, string> Settings);

public record RedditAuthUrlResponse(string AuthUrl);

public record AuthMeResponse(int Id, string Email, string? DisplayName);
