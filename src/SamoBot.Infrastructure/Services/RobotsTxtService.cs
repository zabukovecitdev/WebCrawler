using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.Logging;
using RobotsTxtModel = Samobot.Domain.Models.RobotsTxt;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Parsers;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace SamoBot.Infrastructure.Services;

public class RobotsTxtService : IRobotsTxtService
{
    private readonly ICache _cache;
    private readonly IRobotsTxtRepository _repository;
    private readonly IRobotsTxtParser _parser;
    private readonly IUrlFetchService _urlFetchService;
    private readonly ILogger<RobotsTxtService> _logger;

    private static readonly TimeSpan RedisCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan DefaultDbCacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan ErrorCacheTtl = TimeSpan.FromHours(1);
    private const int FetchTimeoutSeconds = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RobotsTxtService(
        ICache cache,
        IRobotsTxtRepository repository,
        IRobotsTxtParser parser,
        IUrlFetchService urlFetchService,
        ILogger<RobotsTxtService> logger)
    {
        _cache = cache;
        _repository = repository;
        _parser = parser;
        _urlFetchService = urlFetchService;
        _logger = logger;
    }

    public async Task<Result<bool>> IsUrlAllowed(string url, string userAgent, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("Invalid URL format: {Url}", url);
            return Result.Ok(true);
        }

        var host = uri.Host;
        var robotsTxtResult = await GetRobotsTxt(host, cancellationToken);

        if (robotsTxtResult.IsFailed)
        {
            _logger.LogWarning("Failed to get robots.txt for {Host}, allowing crawl: {Errors}",
                host, string.Join("; ", robotsTxtResult.Errors.Select(e => e.Message)));
            return Result.Ok(true);
        }

        var robotsTxt = robotsTxtResult.Value;

        if (robotsTxt.IsFetchError)
        {
            return Result.Ok(true);
        }

        var isAllowed = _parser.IsUrlAllowed(robotsTxt, url, userAgent);
        
        return Result.Ok(isAllowed);
    }

    public async Task<Result<int?>> GetCrawlDelayMs(string host, CancellationToken cancellationToken = default)
    {
        var robotsTxtResult = await GetRobotsTxt(host, cancellationToken);

        if (robotsTxtResult.IsFailed)
        {
            return Result.Ok<int?>(null);
        }

        return Result.Ok(robotsTxtResult.Value.CrawlDelayMs);
    }

    public async Task<Result<RobotsTxtModel>> GetRobotsTxt(string host, CancellationToken cancellationToken = default)
    {
        var redisResult = await GetFromRedis(host, cancellationToken);
        if (redisResult.IsSuccess && redisResult.Value != null)
        {
            _logger.LogDebug("Cache hit (Redis) for robots.txt: {Host}", host);
            return Result.Ok(redisResult.Value);
        }

        var dbResult = await GetFromDatabase(host, cancellationToken);
        if (dbResult.IsSuccess && dbResult.Value != null)
        {
            _logger.LogDebug("Cache hit (DB) for robots.txt: {Host}", host);

            await SetInRedis(dbResult.Value, cancellationToken);

            return Result.Ok(dbResult.Value);
        }

        // Tier 3: Fetch from HTTP
        _logger.LogInformation("Cache miss for robots.txt, fetching from HTTP: {Host}", host);
        return await FetchAndCache(host, cancellationToken);
    }

    private async Task<Result<RobotsTxtModel?>> GetFromRedis(string host, CancellationToken cancellationToken)
    {
        try
        {
            var key = GetRedisCacheKey(host);
            var result = await _cache.Get(key, cancellationToken);

            if (result.IsFailed || string.IsNullOrEmpty(result.Value))
            {
                return Result.Ok<RobotsTxtModel?>(null);
            }

            var robotsTxt = JsonSerializer.Deserialize<RobotsTxtModel?>(result.Value, JsonOptions);
            return Result.Ok(robotsTxt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get robots.txt from Redis for host {Host}", host);
            return Result.Ok<RobotsTxtModel?>(null); // Non-critical failure
        }
    }

    private async Task<Result<RobotsTxtModel?>> GetFromDatabase(string host, CancellationToken cancellationToken)
    {
        try
        {
            var robotsTxt = await _repository.GetByHostAsync(host, cancellationToken);
            return Result.Ok(robotsTxt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get robots.txt from database for host {Host}", host);
            return Result.Fail<RobotsTxtModel?>($"Database error: {ex.Message}");
        }
    }

    private async Task<Result> SetInRedis(RobotsTxtModel robotsTxt, CancellationToken cancellationToken)
    {
        try
        {
            var key = GetRedisCacheKey(robotsTxt.Host);
            var json = JsonSerializer.Serialize(robotsTxt, JsonOptions);
            return await _cache.Set(key, json, RedisCacheTtl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache robots.txt in Redis for host {Host}", robotsTxt.Host);
            return Result.Ok(); // Non-critical failure
        }
    }

    private async Task<Result<RobotsTxtModel>> FetchAndCache(string host, CancellationToken cancellationToken)
    {
        var robotsTxtUrl = $"https://{host}/robots.txt";

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(FetchTimeoutSeconds));

            var fetchedContent = await _urlFetchService.Fetch(robotsTxtUrl, cts.Token);

            RobotsTxtModel robotsTxt;
            var now = DateTime.UtcNow;

            if (fetchedContent.StatusCode == 404)
            {
                // No robots.txt = allow all
                _logger.LogInformation("No robots.txt found for {Host} (404), allowing all crawling", host);
                robotsTxt = CreateAllowAllRobotsTxt(host, now, DefaultDbCacheTtl);
                robotsTxt.StatusCode = 404;
            }
            else if (fetchedContent.StatusCode >= 200 && fetchedContent.StatusCode < 300)
            {
                // Success - parse the content
                var content = fetchedContent.ContentBytes != null
                    ? Encoding.UTF8.GetString(fetchedContent.ContentBytes)
                    : string.Empty;

                var parseResult = _parser.Parse(content, host, now, DefaultDbCacheTtl);

                if (parseResult.IsFailed)
                {
                    _logger.LogWarning("Failed to parse robots.txt for {Host}, allowing all crawling", host);
                    robotsTxt = CreateAllowAllRobotsTxt(host, now, DefaultDbCacheTtl);
                    robotsTxt.IsFetchError = true;
                    robotsTxt.ErrorMessage = string.Join("; ", parseResult.Errors.Select(e => e.Message));
                    robotsTxt.StatusCode = fetchedContent.StatusCode;
                }
                else
                {
                    robotsTxt = parseResult.Value;
                    robotsTxt.StatusCode = fetchedContent.StatusCode;
                }
            }
            else
            {
                // Error status code - cache as error with short TTL
                _logger.LogWarning("Failed to fetch robots.txt for {Host} (status {StatusCode}), allowing all crawling",
                    host, fetchedContent.StatusCode);
                robotsTxt = CreateAllowAllRobotsTxt(host, now, ErrorCacheTtl);
                robotsTxt.IsFetchError = true;
                robotsTxt.ErrorMessage = fetchedContent.Error ?? $"HTTP {fetchedContent.StatusCode}";
                robotsTxt.StatusCode = fetchedContent.StatusCode;
            }

            // Save to database and Redis
            await _repository.SaveAsync(robotsTxt, cancellationToken);
            await SetInRedis(robotsTxt, cancellationToken);

            return Result.Ok(robotsTxt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Propagate user cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch robots.txt for {Host}, allowing all crawling", host);

            // Try to use expired cache entry if available
            try
            {
                var expiredEntry = await GetExpiredEntryAsync(host, cancellationToken);
                if (expiredEntry != null)
                {
                    _logger.LogInformation("Using expired robots.txt cache for {Host}", host);
                    return Result.Ok(expiredEntry);
                }
            }
            catch (Exception cacheEx)
            {
                _logger.LogWarning(cacheEx, "Failed to retrieve expired cache entry for {Host}", host);
            }

            // Create and cache allow-all entry with short TTL
            var robotsTxt = CreateAllowAllRobotsTxt(host, DateTime.UtcNow, ErrorCacheTtl);
            robotsTxt.IsFetchError = true;
            robotsTxt.ErrorMessage = ex.Message;

            await _repository.SaveAsync(robotsTxt, cancellationToken);
            await SetInRedis(robotsTxt, cancellationToken);

            return Result.Ok(robotsTxt);
        }
    }

    private async Task<RobotsTxtModel?> GetExpiredEntryAsync(string host, CancellationToken cancellationToken)
    {
        // Query database without expiration filter to get expired entries
        var result = await _repository.GetByHostAsync(host, cancellationToken);
        return result;
    }

    private static RobotsTxtModel CreateAllowAllRobotsTxt(string host, DateTime fetchedAt, TimeSpan ttl)
    {
        return new RobotsTxtModel
        {
            Host = host,
            Content = "# No robots.txt or error fetching",
            Rules = new List<RobotsTxtRule>(),
            FetchedAt = fetchedAt,
            ExpiresAt = fetchedAt.Add(ttl),
            IsFetchError = false
        };
    }

    private static string GetRedisCacheKey(string host) => $"robots:host:{host}";
}
