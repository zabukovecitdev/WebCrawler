using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Services.Abstractions;

namespace SamoBot.Api.Controllers;

[ApiController]
[Route("api/v1/crawl-jobs")]
[Authorize]
public class CrawlJobsController : ControllerBase
{
    private readonly ICrawlJobService _crawlJobService;
    private readonly ICrawlJobRepository _jobs;
    private readonly IDiscoveredUrlRepository _discoveredUrls;
    private readonly ICrawlJobEventRepository _events;

    public CrawlJobsController(
        ICrawlJobService crawlJobService,
        ICrawlJobRepository jobs,
        IDiscoveredUrlRepository discoveredUrls,
        ICrawlJobEventRepository events)
    {
        _crawlJobService = crawlJobService;
        _jobs = jobs;
        _discoveredUrls = discoveredUrls;
        _events = events;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var jobs = await _jobs.ListRecent(Math.Clamp(limit, 1, 200), cancellationToken);
        return Ok(jobs);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken = default)
    {
        var job = await _crawlJobService.GetAsync(id, cancellationToken);
        return job == null ? NotFound() : Ok(job);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCrawlJobRequest request, CancellationToken cancellationToken = default)
    {
        var owner = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var job = await _crawlJobService.CreateAsync(
            owner,
            request.SeedUrls,
            request.MaxDepth,
            request.MaxUrls,
            request.UseJsRendering,
            request.RespectRobots,
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = job.Id }, job);
    }

    [HttpPost("{id:int}/actions")]
    public async Task<IActionResult> Actions(int id, [FromBody] CrawlJobActionRequest request, CancellationToken cancellationToken = default)
    {
        var ok = request.Action.ToLowerInvariant() switch
        {
            "start" => await _crawlJobService.StartAsync(id, cancellationToken),
            "pause" => await _crawlJobService.PauseAsync(id, cancellationToken),
            "cancel" => await _crawlJobService.CancelAsync(id, cancellationToken),
            _ => false
        };

        return ok ? Ok() : BadRequest();
    }

    [HttpGet("{id:int}/pages")]
    public async Task<IActionResult> Pages(int id, [FromQuery] int limit = 50, [FromQuery] int offset = 0, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetById(id, cancellationToken);
        if (job == null)
        {
            return NotFound();
        }

        var pages = await _discoveredUrls.GetByCrawlJobId(id, Math.Clamp(limit, 1, 200), Math.Max(0, offset), cancellationToken);
        var total = await _discoveredUrls.CountByCrawlJobId(id, cancellationToken);
        return Ok(new { total, items = pages });
    }

    [HttpGet("{id:int}/events")]
    public async Task<IActionResult> Events(int id, [FromQuery] long after = 0, [FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetById(id, cancellationToken);
        if (job == null)
        {
            return NotFound();
        }

        var items = await _events.GetAfter(id, after, Math.Clamp(limit, 1, 500), cancellationToken);
        return Ok(items);
    }
}

public class CreateCrawlJobRequest
{
    public List<string> SeedUrls { get; set; } = [];
    public int? MaxDepth { get; set; }
    public int? MaxUrls { get; set; }
    public bool UseJsRendering { get; set; }
    public bool RespectRobots { get; set; } = true;
}

public class CrawlJobActionRequest
{
    public string Action { get; set; } = string.Empty;
}
