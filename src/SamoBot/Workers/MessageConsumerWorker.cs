using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Samobot.Domain.Models;
using SamoBot.Extensions;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Data;
using SamoBot.Utilities;

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

            await ProcessMessageAsync(args.Message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {Message}", args.Message);
        }
    }

    private async Task ProcessMessageAsync(string dirtyUrl, CancellationToken cancellationToken = default)
    {
        if (!UrlNormalizer.TryClean(dirtyUrl, out var normalizedUrl) || normalizedUrl == null)
        {
            _logger.LogWarning("Message is not a valid URL: {Message}", dirtyUrl);
            
            return;
        }

        _logger.LogInformation("Processing URL: {Url}", normalizedUrl);

        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDiscoveredUrlRepository>();

        // TODO: This check could be more optimised with Bloom filter
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
            DiscoveredAt = _timeProvider.GetUtcNow(),
            Priority = normalizedUrl.GetUrlSegmentsLength() + normalizedUrl.GetQueryParameterCount()
        };

        var id = await repository.Insert(discoveredUrl, cancellationToken);
        
        _logger.LogInformation("Inserted discovered URL with ID: {Id}, URL: {Url}", id, normalizedUrl);
    }

    public override void Dispose()
    {
        _messageConsumer.Dispose();
        base.Dispose();
    }
}
