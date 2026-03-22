using AINews.Api.Data;
using AINews.Api.DTOs;
using AINews.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AINews.Api.Controllers;

[ApiController]
[Route("api/topics")]
[Authorize]
public class TopicsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var topics = await db.Topics
            .Select(t => new TopicDto(
                t.Id, t.Name, t.Description, t.CreatedAt,
                t.Sources.Count))
            .ToListAsync();
        return Ok(topics);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var t = await db.Topics.FindAsync(id);
        if (t == null) return NotFound();
        var count = await db.Sources.CountAsync(s => s.TopicId == id);
        return Ok(new TopicDto(t.Id, t.Name, t.Description, t.CreatedAt, count));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTopicRequest req)
    {
        var topic = new Topic
        {
            Name = req.Name,
            Description = req.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = topic.Id },
            new TopicDto(topic.Id, topic.Name, topic.Description, topic.CreatedAt, 0));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTopicRequest req)
    {
        var topic = await db.Topics.FindAsync(id);
        if (topic == null) return NotFound();
        topic.Name = req.Name;
        topic.Description = req.Description;
        topic.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var count = await db.Sources.CountAsync(s => s.TopicId == id);
        return Ok(new TopicDto(topic.Id, topic.Name, topic.Description, topic.CreatedAt, count));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var topic = await db.Topics.FindAsync(id);
        if (topic == null) return NotFound();
        db.Topics.Remove(topic);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
