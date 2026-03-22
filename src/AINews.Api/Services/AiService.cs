using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AINews.Api.Services;

public class AiService(SettingsService settings, IHttpClientFactory httpFactory, ILogger<AiService> logger)
{
    private const string DefaultModel = "gpt-4o-mini";

    public record PostAnalysis(
        string Summary,
        string[] Insights,
        double Relevance,
        bool ShouldInclude);

    public record LinkSummary(string Title, string Summary);

    public async Task<PostAnalysis?> AnalyzePostAsync(string title, string? body, IEnumerable<string> linkUrls)
    {
        var linksStr = string.Join(", ", linkUrls.Take(5));
        var prompt = "Analyze this social media post and extract useful information for a developer news feed.\n\n" +
                     $"Title: {title}\nBody: {body ?? "(no body)"}\nLinks found: {linksStr}\n\n" +
                     "Respond with JSON only:\n" +
                     "{\"summary\":\"2-3 sentence summary\",\"insights\":[\"insight 1\"],\"relevance\":0.7,\"shouldInclude\":true}\n\n" +
                     "Set shouldInclude=false if the post is off-topic, spam, or has no useful developer content.";

        var result = await CallAiAsync<PostAnalysisResponse>(prompt);
        if (result == null) return null;

        return new PostAnalysis(
            result.Summary ?? string.Empty,
            result.Insights ?? [],
            result.Relevance,
            result.ShouldInclude);
    }

    public async Task<LinkSummary?> SummarizeLinkAsync(string url, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var snippet = content[..Math.Min(content.Length, 3000)];
        var prompt = $"Summarize this content from {url} in 2-3 sentences for a developer news feed.\n" +
                     "Focus on: what it is, why it's useful, key features.\n\n" +
                     $"Content: {snippet}\n\n" +
                     "Respond with JSON only:\n" +
                     "{\"title\":\"short descriptive title\",\"summary\":\"2-3 sentence summary\"}";

        var result = await CallAiAsync<LinkSummaryResponse>(prompt);
        if (result == null) return null;

        return new LinkSummary(result.Title ?? url, result.Summary ?? string.Empty);
    }

    private async Task<T?> CallAiAsync<T>(string prompt) where T : class
    {
        // Try Z.ai first, fall back to OpenAI
        var (zaiKey, zaiBase) = await settings.GetZAiCredentialsAsync();
        if (!string.IsNullOrEmpty(zaiKey) && !string.IsNullOrEmpty(zaiBase))
        {
            var result = await CallOpenAiCompatibleAsync<T>(prompt, zaiBase, zaiKey);
            if (result != null) return result;
            logger.LogWarning("Z.ai call failed, falling back to OpenAI");
        }

        var openAiKey = await settings.GetOpenAiApiKeyAsync();
        if (!string.IsNullOrEmpty(openAiKey))
            return await CallOpenAiCompatibleAsync<T>(prompt, "https://api.openai.com", openAiKey);

        logger.LogWarning("No AI provider configured");
        return null;
    }

    private async Task<T?> CallOpenAiCompatibleAsync<T>(string prompt, string baseUrl, string apiKey) where T : class
    {
        try
        {
            using var http = httpFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            http.Timeout = TimeSpan.FromSeconds(30);

            var requestUrl = baseUrl.TrimEnd('/') + "/v1/chat/completions";
            var requestBody = new
            {
                model = DefaultModel,
                messages = new[] { new { role = "user", content = prompt } },
                response_format = new { type = "json_object" },
                temperature = 0.3,
            };

            var response = await http.PostAsJsonAsync(requestUrl, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI API returned {Status} from {Base}", response.StatusCode, baseUrl);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<OpenAiResponse>(responseJson, _jsonOptions);
            var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
            if (content == null) return null;

            return JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI call failed for {Base}", baseUrl);
            return null;
        }
    }

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class PostAnalysisResponse
    {
        public string? Summary { get; set; }
        public string[]? Insights { get; set; }
        public double Relevance { get; set; }
        [JsonPropertyName("shouldInclude")]
        public bool ShouldInclude { get; set; }
    }

    private sealed class LinkSummaryResponse
    {
        public string? Title { get; set; }
        public string? Summary { get; set; }
    }

    private sealed class OpenAiResponse
    {
        public List<OpenAiChoice>? Choices { get; set; }
    }
    private sealed class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
    }
    private sealed class OpenAiMessage
    {
        public string? Content { get; set; }
    }
}
