using AINews.Api.Data;
using AINews.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AINews.Api.Controllers;

[ApiController]
[Route("api/news")]
[Authorize]
public class NewsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? topicId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] DateTime? since = null)
    {
        var q = db.NewsItems
            .Include(n => n.Source)
            .Include(n => n.Topic)
            .Include(n => n.RawPost)
            .Include(n => n.ExtractedLinks)
            .AsQueryable();

        if (topicId.HasValue) q = q.Where(n => n.TopicId == topicId.Value);
        if (since.HasValue) q = q.Where(n => n.PublishedAt >= since.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(n => n.PublishedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        var dtos = items.Select(n => new NewsItemDto(
            n.Id,
            n.TopicId,
            n.Topic.Name,
            n.Source.Type,
            n.Source.DisplayName,
            n.Title,
            n.AiSummary,
            TryParseInsights(n.AiInsights),
            n.Relevance,
            n.PublishedAt,
            n.RawPost.ExternalUrl,
            n.RawPost.Author,
            n.ExtractedLinks.Select(l => new ExtractedLinkDto(
                l.Id, l.Url, l.LinkType, l.Title, l.Summary, l.FetchStatus))));

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

        if (n == null) return NotFound();

        return Ok(new NewsItemDto(
            n.Id, n.TopicId, n.Topic.Name, n.Source.Type, n.Source.DisplayName,
            n.Title, n.AiSummary, TryParseInsights(n.AiInsights), n.Relevance, n.PublishedAt,
            n.RawPost.ExternalUrl, n.RawPost.Author,
            n.ExtractedLinks.Select(l => new ExtractedLinkDto(l.Id, l.Url, l.LinkType, l.Title, l.Summary, l.FetchStatus))));
    }

    private static string[]? TryParseInsights(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<string[]>(json); }
        catch { return null; }
    }
}
