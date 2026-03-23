using AINews.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AINews.Api.Services;

/// <summary>
/// Builds a natural-language preference context from the user's feedback history,
/// injected into AI prompts so the scan engine learns what the user likes/dislikes.
/// </summary>
public class PreferenceService(AppDbContext db)
{
    private const int MaxExamples = 10;

    public async Task<string?> BuildContextAsync()
    {
        var liked = await db.NewsItems
            .Where(n => n.UserFeedback == "Liked")
            .OrderByDescending(n => n.FeedbackAt)
            .Take(MaxExamples)
            .Select(n => n.Title)
            .ToListAsync();

        var disliked = await db.NewsItems
            .Where(n => n.UserFeedback == "Disliked")
            .OrderByDescending(n => n.FeedbackAt)
            .Take(MaxExamples)
            .Select(n => n.Title)
            .ToListAsync();

        if (!liked.Any() && !disliked.Any()) return null;

        var parts = new List<string>();

        if (liked.Any())
            parts.Add("Posts the user liked (high relevance, include similar):\n" +
                      string.Join("\n", liked.Select(t => $"  - {t}")));

        if (disliked.Any())
            parts.Add("Posts the user disliked (low relevance, exclude similar):\n" +
                      string.Join("\n", disliked.Select(t => $"  - {t}")));

        return string.Join("\n\n", parts);
    }
}
