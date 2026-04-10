using System.Text.Json;
using Microsoft.Extensions.Logging;
using Samobot.Infrastructure.Enums;
using SamoBot.Infrastructure.Data;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Enums;
using SamoBot.Infrastructure.Models;
using SamoBot.Infrastructure.Services.Abstractions;
using SamoBot.Infrastructure.Extensions;
using SamoBot.Infrastructure.Utilities;

namespace SamoBot.Infrastructure.Services;

public class CrawlJobService : ICrawlJobService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ICrawlJobRepository _jobs;
    private readonly IDiscoveredUrlRepository _discoveredUrls;
    private readonly ICrawlTelemetryService _telemetry;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CrawlJobService> _logger;

    public CrawlJobService(
        ICrawlJobRepository jobs,
        IDiscoveredUrlRepository discoveredUrls,
        ICrawlTelemetryService telemetry,
        TimeProvider timeProvider,
        ILogger<CrawlJobService> logger)
    {
        _jobs = jobs;
        _discoveredUrls = discoveredUrls;
        _telemetry = telemetry;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<CrawlJobEntity> CreateAsync(
        string? ownerUserId,
        IReadOnlyList<string> seedUrls,
        int? maxDepth,
        int? maxUrls,
        bool useJsRendering,
        bool respectRobots,
        CancellationToken cancellationToken = default)
    {
        var list = seedUrls.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("At least one seed URL is required", nameof(seedUrls));
        }

        var entity = new CrawlJobEntity
        {
            OwnerUserId = ownerUserId,
            Status = CrawlJobStatus.Pending.AsString(),
            SeedUrls = JsonSerializer.Serialize(list, JsonOptions),
            MaxDepth = maxDepth,
            MaxUrls = maxUrls,
            UseJsRendering = useJsRendering,
            RespectRobots = respectRobots
        };

        var id = await _jobs.Insert(entity, cancellationToken);
        entity.Id = id;
        await _telemetry.PublishAsync(id, "JobCreated", new { seedCount = list.Count }, cancellationToken);
        return entity;
    }

    public async Task<bool> StartAsync(int id, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetById(id, cancellationToken);
        if (job == null)
        {
            return false;
        }

        var status = job.GetStatus();
        if (status is not CrawlJobStatus.Draft and not CrawlJobStatus.Pending and not CrawlJobStatus.Paused)
        {
            return false;
        }

        var seeds = JsonSerializer.Deserialize<List<string>>(job.SeedUrls, JsonOptions) ?? [];
        var now = _timeProvider.GetUtcNow();
        job.Status = CrawlJobStatus.Running.AsString();
        job.StartedAt ??= now;
        job.CompletedAt = null;
        job.UpdatedAt = now;
        await _jobs.Update(job, cancellationToken);

        foreach (var raw in seeds)
        {
            if (!UrlNormalizer.TryClean(raw, out var normalized) || normalized == null)
            {
                _logger.LogWarning("Skipping invalid seed URL: {Url}", raw);
                continue;
            }

            var exists = await _discoveredUrls.Exists(normalized.AbsoluteUri, cancellationToken);
            if (exists)
            {
                continue;
            }

            var discovered = new DiscoveredUrl
            {
                Url = raw,
                Host = normalized.Host,
                NormalizedUrl = normalized.AbsoluteUri,
                DiscoveredAt = now.ToUniversalTime(),
                Priority = normalized.GetUrlSegmentsLength() + normalized.GetQueryParameterCount(),
                Status = UrlStatus.Idle,
                CrawlJobId = id,
                Depth = 0,
                UseJsRendering = job.UseJsRendering,
                RespectRobots = job.RespectRobots
            };

            await _discoveredUrls.Insert(discovered, cancellationToken);
        }

        await _telemetry.PublishAsync(id, "JobStarted", new { at = now }, cancellationToken);
        return true;
    }

    public async Task<bool> PauseAsync(int id, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetById(id, cancellationToken);
        if (job == null || job.GetStatus() != CrawlJobStatus.Running)
        {
            return false;
        }

        job.Status = CrawlJobStatus.Paused.AsString();
        job.UpdatedAt = _timeProvider.GetUtcNow();
        await _jobs.Update(job, cancellationToken);
        await _telemetry.PublishAsync(id, "JobPaused", new { }, cancellationToken);
        return true;
    }

    public async Task<bool> CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetById(id, cancellationToken);
        if (job == null)
        {
            return false;
        }

        job.Status = CrawlJobStatus.Cancelled.AsString();
        job.CompletedAt = _timeProvider.GetUtcNow();
        job.UpdatedAt = job.CompletedAt.Value;
        await _jobs.Update(job, cancellationToken);
        await _telemetry.PublishAsync(id, "JobCancelled", new { }, cancellationToken);
        return true;
    }

    public Task<CrawlJobEntity?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        return _jobs.GetById(id, cancellationToken);
    }

    public async Task<CrawlJobStatus?> GetStatusAsync(int id, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetById(id, cancellationToken);
        return job?.GetStatus();
    }
}
