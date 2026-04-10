using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Extensions;
using SamoBot.Infrastructure.Models;
using SamoBot.Infrastructure.Utilities;

namespace SamoBot.Workers;

public class MessageConsumerWorker : BackgroundService
{
    private readonly ILogger<MessageConsumerWorker> _logger;
    private readonly IMessageConsumer _messageConsumer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TimeProvider _timeProvider;

    public MessageConsumerWorker(
        ILogger<MessageConsumerWorker> logger,
        IMessageConsumer messageConsumer,
        IServiceScopeFactory serviceScopeFactory,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _messageConsumer = messageConsumer;
        _serviceScopeFactory = serviceScopeFactory;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _messageConsumer.MessageReceived += OnMessageReceived;

        await _messageConsumer.StartAsync(stoppingToken);
        _logger.LogInformation("Message consumer started.");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Message consumer worker cancellation requested.");
        }
        finally
        {
            _messageConsumer.MessageReceived -= OnMessageReceived;
            await _messageConsumer.StopAsync(stoppingToken);
            _logger.LogInformation("Message consumer worker stopped.");
        }
    }

    private async void OnMessageReceived(object? sender, MessageReceivedEventArgs args)
    {
        try
        {
            _logger.LogInformation(
                "Received message from {Source}. CreatedAt: {CreatedAt}, Message: {Message}",
                sender?.GetType().Name ?? "Unknown",
                args.CreatedAt,
                args.Message);

            await ProcessMessage(args.Message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {Message}", args.Message);
        }
    }

    private async Task ProcessMessage(string rawMessage, CancellationToken cancellationToken = default)
    {
        var discovery = TryParseDiscovery(rawMessage);
        var dirtyUrl = discovery?.Url ?? rawMessage;

        if (!UrlNormalizer.TryClean(dirtyUrl, out var normalizedUrl) || normalizedUrl == null)
        {
            _logger.LogWarning("Message is not a valid URL: {Message}", rawMessage);

            return;
        }

        _logger.LogInformation("Processing URL: {Url}", normalizedUrl);

        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDiscoveredUrlRepository>();
        var crawlJobs = scope.ServiceProvider.GetService<ICrawlJobRepository>();

        if (discovery?.CrawlJobId is { } jobId && crawlJobs != null)
        {
            var job = await crawlJobs.GetById(jobId, cancellationToken);
            if (job == null)
            {
                _logger.LogWarning("Crawl job {JobId} not found for discovery message", jobId);
                return;
            }

            if (job.MaxDepth.HasValue && discovery.Depth > job.MaxDepth.Value)
            {
                _logger.LogDebug("Skipping URL beyond MaxDepth: {Url} depth {Depth}", dirtyUrl, discovery.Depth);
                return;
            }

            if (job.MaxUrls.HasValue)
            {
                var count = await repository.CountByCrawlJobId(jobId, cancellationToken);
                if (count >= job.MaxUrls.Value)
                {
                    _logger.LogDebug("Skipping URL: job {JobId} reached MaxUrls", jobId);
                    return;
                }
            }
        }

        var exists = await repository.Exists(normalizedUrl.AbsoluteUri, cancellationToken);
        if (exists)
        {
            _logger.LogInformation("URL already exists in database: {Url}", normalizedUrl);

            return;
        }

        var discoveredUrl = new DiscoveredUrl
        {
            Url = dirtyUrl,
            Host = normalizedUrl.Host,
            NormalizedUrl = normalizedUrl.AbsoluteUri,
            DiscoveredAt = _timeProvider.GetUtcNow().ToUniversalTime(),
            Priority = normalizedUrl.GetUrlSegmentsLength() + normalizedUrl.GetQueryParameterCount(),
            CrawlJobId = discovery?.CrawlJobId,
            Depth = discovery?.Depth ?? 0,
            UseJsRendering = discovery?.UseJsRendering ?? false,
            RespectRobots = discovery?.RespectRobots ?? true
        };

        var id = await repository.Insert(discoveredUrl, cancellationToken);

        _logger.LogInformation("Inserted discovered URL with ID: {Id}, URL: {Url}", id, normalizedUrl);
    }

    private static UrlDiscoveryMessage? TryParseDiscovery(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage) || !rawMessage.TrimStart().StartsWith('{'))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UrlDiscoveryMessage>(rawMessage);
        }
        catch
        {
            return null;
        }
    }

    public override void Dispose()
    {
        _messageConsumer.Dispose();

        base.Dispose();
    }
}
