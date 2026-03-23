using AINews.Api.Data;
using AINews.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AINews.Api.Controllers;

[ApiController]
[Route("api/news")]
public class NewsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? topicId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] DateTime? since = null,
        [FromQuery] bool includeDisliked = false)
    {
        var q = db.NewsItems
            .Include(n => n.Source)
            .Include(n => n.Topic)
            .Include(n => n.RawPost)
            .Include(n => n.ExtractedLinks)
            .AsQueryable();

        if (topicId.HasValue) q = q.Where(n => n.TopicId == topicId.Value);
        if (since.HasValue) q = q.Where(n => n.PublishedAt >= since.Value);
        if (!includeDisliked) q = q.Where(n => n.UserFeedback != "Disliked");

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(n => n.PublishedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        var dtos = items.Select(ToDto);
        return Ok(new { items = dtos, total, page, pageSize = limit });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var n = await db.NewsItems
            .Include(n => n.Source)
            .Include(n => n.Topic)
            .Include(n => n.RawPost)
            .Include(n => n.ExtractedLinks)
            .FirstOrDefaultAsync(n => n.Id == id);

        return n == null ? NotFound() : Ok(ToDto(n));
    }

    [HttpPatch("{id}/feedback")]
    public async Task<IActionResult> Feedback(int id, [FromBody] FeedbackRequest req)
    {
        if (req.Feedback is not null and not "Liked" and not "Disliked")
            return BadRequest("Feedback must be 'Liked', 'Disliked', or null.");

        var n = await db.NewsItems.FindAsync(id);
        if (n == null) return NotFound();

        n.UserFeedback = req.Feedback;
        n.FeedbackAt = req.Feedback != null ? DateTime.UtcNow : null;
        await db.SaveChangesAsync();
        return Ok(new { id, userFeedback = n.UserFeedback });
    }

    private static NewsItemDto ToDto(AINews.Api.Models.NewsItem n) => new(
        n.Id, n.TopicId, n.Topic.Name, n.Source.Type, n.Source.DisplayName,
        n.Title, n.AiSummary, TryParseInsights(n.AiInsights), n.Relevance, n.PublishedAt,
        n.RawPost.ExternalUrl, n.RawPost.Author,
        n.ExtractedLinks.Select(l => new ExtractedLinkDto(l.Id, l.Url, l.LinkType, l.Title, l.Summary, l.FetchStatus)),
        n.UserFeedback);

    private static string[]? TryParseInsights(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<string[]>(json); }
        catch { return null; }
    }
}
