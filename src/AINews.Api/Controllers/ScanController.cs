using AINews.Api.BackgroundServices;
using AINews.Api.Data;
using AINews.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AINews.Api.Controllers;

[ApiController]
[Route("api/scan")]
public class ScanController(AppDbContext db, ScanBackgroundService scanner) : ControllerBase
{
    [HttpPost("trigger")]
    public IActionResult Trigger()
    {
        if (scanner.IsRunning)
            return Conflict(new { message = "A scan is already running.", scanRunId = scanner.CurrentScanRunId });

        scanner.TryEnqueue();
        return Accepted(new TriggerScanResponse(scanner.CurrentScanRunId ?? 0));
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        if (scanner.IsRunning && scanner.CurrentScanRunId.HasValue)
        {
            var run = await db.ScanRuns.FindAsync(scanner.CurrentScanRunId.Value);
            return Ok(run == null ? null : ToDto(run));
        }
        var latest = await db.ScanRuns.OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync();
        return Ok(latest == null ? null : ToDto(latest));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        var runs = await db.ScanRuns
            .OrderByDescending(r => r.StartedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();
        return Ok(runs.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var run = await db.ScanRuns.FindAsync(id);
        return run == null ? NotFound() : Ok(ToDto(run));
    }

    private static ScanRunDto ToDto(AINews.Api.Models.ScanRun r) =>
        new(r.Id, r.Status, r.StartedAt, r.CompletedAt,
            r.TotalSourcesScanned, r.TotalRawPostsFetched, r.TotalNewsItemsCreated, r.ErrorMessage);
}
