using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamoBot.Infrastructure.Services.Abstractions;

namespace SamoBot.Api.Controllers;

[ApiController]
[Route("api/v1/hosts")]
[Authorize]
public class HostsController : ControllerBase
{
    private readonly IRobotsTxtService _robotsTxtService;

    public HostsController(IRobotsTxtService robotsTxtService)
    {
        _robotsTxtService = robotsTxtService;
    }

    [HttpGet("{host}/robots")]
    public async Task<IActionResult> GetRobots(string host, CancellationToken cancellationToken)
    {
        var result = await _robotsTxtService.GetRobotsTxt(host, cancellationToken);
        return result.IsFailed ? BadRequest(result.Errors) : Ok(result.Value);
    }

    [HttpGet("{host}/sitemaps")]
    public IActionResult GetSitemaps(string host)
    {
        return Ok(new
        {
            host,
            message = "Sitemap discovery and persistence are planned; no stored sitemaps yet.",
            urls = Array.Empty<string>()
        });
    }

    [HttpPost("explain")]
    public async Task<IActionResult> Explain([FromBody] ExplainRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
        {
            return BadRequest("Invalid URL.");
        }

        const string userAgent = "SamoBot";
        var allowed = await _robotsTxtService.IsUrlAllowed(request.Url, userAgent, cancellationToken);
        var crawlDelay = await _robotsTxtService.GetCrawlDelayMs(uri.Host, cancellationToken);
        var robots = await _robotsTxtService.GetRobotsTxt(uri.Host, cancellationToken);

        return Ok(new
        {
            url = request.Url,
            host = uri.Host,
            userAgent,
            allowedByRobots = allowed.IsSuccess ? allowed.Value : (bool?)null,
            robotsErrors = allowed.IsFailed ? string.Join("; ", allowed.Errors.Select(e => e.Message)) : null,
            crawlDelayMs = crawlDelay.IsSuccess ? crawlDelay.Value : null,
            robotsTxt = robots.IsSuccess ? robots.Value : null
        });
    }
}

public class ExplainRequest
{
    public string Url { get; set; } = string.Empty;
}
