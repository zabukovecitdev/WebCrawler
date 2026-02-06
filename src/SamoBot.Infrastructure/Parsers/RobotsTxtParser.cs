using FluentResults;
using Microsoft.Extensions.Logging;
using SamoBot.Infrastructure.Models;
using RobotsTxtModel = SamoBot.Infrastructure.Models.RobotsTxt;

namespace SamoBot.Infrastructure.Parsers;

public class RobotsTxtParser : IRobotsTxtParser
{
    private readonly ILogger<RobotsTxtParser> _logger;
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(24);

    public RobotsTxtParser(ILogger<RobotsTxtParser> logger)
    {
        _logger = logger;
    }

    public Result<RobotsTxtModel> Parse(string content, string host, DateTime fetchedAt, TimeSpan? cacheTtl = null)
    {
        try
        {
            var robots = global::RobotsTxtParser.Robots.Load(content);

            int? crawlDelayMs = null;
            try
            {
                var delay = robots.CrawlDelay("*");
                if (delay > TimeSpan.Zero)
                {
                    crawlDelayMs = (int)delay.TotalMilliseconds;
                }
            }
            catch
            {
            }

            var sitemapUrls = robots.Sitemaps
                .Where(s => s.Url != null)
                .Select(s => s.Url!.ToString())
                .ToList();

            var ttl = cacheTtl ?? DefaultCacheTtl;

            return Result.Ok(new RobotsTxtModel
            {
                Host = host,
                Content = content,
                Rules = new List<RobotsTxtRule>(),
                CrawlDelayMs = crawlDelayMs,
                SitemapUrls = sitemapUrls,
                FetchedAt = fetchedAt,
                ExpiresAt = fetchedAt.Add(ttl)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse robots.txt for host {Host}", host);
            return Result.Fail<RobotsTxtModel>($"Failed to parse robots.txt: {ex.Message}");
        }
    }

    public bool IsUrlAllowed(RobotsTxtModel robotsTxt, string url, string userAgent)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                _logger.LogWarning("Invalid URL format: {Url}", url);
                return true;
            }

            var robots = global::RobotsTxtParser.Robots.Load(robotsTxt.Content);
            var path = uri.PathAndQuery;
            return robots.IsPathAllowed(userAgent, path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking URL permission for {Url}, allowing by default", url);
            return true;
        }
    }
}
