using AINews.Api.Data;
using AINews.Api.DTOs;
using AINews.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AINews.Api.Controllers;

[ApiController]
[Route("api/sources")]
[Authorize]
public class SourcesController(AppDbContext db) : ControllerBase
{
    private static SourceDto ToDto(Source s) =>
        new(s.Id, s.TopicId, s.Type, s.DisplayName, s.Config, s.IsActive, s.LastScannedAt, s.CreatedAt);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? topicId)
    {
        var q = db.Sources.AsQueryable();
        if (topicId.HasValue) q = q.Where(s => s.TopicId == topicId.Value);
        var sources = await q.OrderBy(s => s.CreatedAt).Select(s => ToDto(s)).ToListAsync();
        return Ok(sources);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSourceRequest req)
    {
        if (!await db.Topics.AnyAsync(t => t.Id == req.TopicId))
            return BadRequest("Topic not found.");

        var source = new Source
        {
            TopicId = req.TopicId,
            Type = req.Type,
            DisplayName = req.DisplayName,
            Config = req.Config,
            CreatedAt = DateTime.UtcNow,
        };
        db.Sources.Add(source);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(List), ToDto(source));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSourceRequest req)
    {
        var source = await db.Sources.FindAsync(id);
        if (source == null) return NotFound();
        source.DisplayName = req.DisplayName;
        source.Config = req.Config;
        source.IsActive = req.IsActive;
        await db.SaveChangesAsync();
        return Ok(ToDto(source));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var source = await db.Sources.FindAsync(id);
        if (source == null) return NotFound();
        db.Sources.Remove(source);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        var source = await db.Sources.FindAsync(id);
        if (source == null) return NotFound();
        source.IsActive = !source.IsActive;
        await db.SaveChangesAsync();
        return Ok(ToDto(source));
    }
}
