using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Samobot.Domain.Enums;
using Samobot.Domain.Models;
using Samobot.Crawler.Utilities;
using SamoBot.Infrastructure.Data;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.RabbitMQ;
using SamoBot.Infrastructure.Storage.Services;

namespace Samobot.Crawler.Workers;

public class CrawlerWorker : BackgroundService
{
    private readonly ILogger<CrawlerWorker> _logger;
    private readonly IOptions<MinioOptions> _minioOptions;
    private readonly RabbitMQConnectionOptions _connectionOptions;
    private readonly ScheduledUrlQueueOptions _queueOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private IConnection? _connection;
    private IModel? _channel;
    private EventingBasicConsumer? _consumer;
    private IStorageManager _storageManager;

    public CrawlerWorker(
        ILogger<CrawlerWorker> logger,
        IOptions<RabbitMQConnectionOptions> connectionOptions,
        IOptions<ScheduledUrlQueueOptions> queueOptions,
        IOptions<MinioOptions> minioOptions,
        IStorageManager storageManager,
        IServiceProvider serviceProvider,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _minioOptions = minioOptions;
        _connectionOptions = connectionOptions.Value;
        _queueOptions = queueOptions.Value;
        _storageManager = storageManager;
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Crawler worker starting...");

        var factory = new ConnectionFactory
        {
            HostName = _connectionOptions.HostName,
            Port = _connectionOptions.Port,
            UserName = _connectionOptions.UserName,
            Password = _connectionOptions.Password,
            VirtualHost = _connectionOptions.VirtualHost
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        RabbitMQSetupHelper.DeclareExchangeAndQueue(_channel, _queueOptions, _logger);

        _consumer = new EventingBasicConsumer(_channel);
        _consumer.Received += async (model, args) =>
        {
            try
            {
                var body = args.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var routingKey = args.RoutingKey;

                _logger.LogInformation("Received message. RoutingKey: {RoutingKey}, Message: {Message}",
                    routingKey, message);

                await ProcessMessage(message, cancellationToken);
                _channel.BasicAck(args.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                _channel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(
            queue: _queueOptions.QueueName,
            autoAck: false,
            consumer: _consumer);

        _logger.LogInformation("Crawler worker started and listening for messages on routing key: {RoutingKey}",
            _queueOptions.RoutingKey);

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Crawler worker stopping...");
        
        _channel?.Close();
        _connection?.Close();
        
        await base.StopAsync(cancellationToken);
    }

    public new void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }

    private async Task ProcessMessage(string message, CancellationToken cancellationToken)
    {
        ScheduledUrl? scheduledUrl;
        try
        {
            scheduledUrl = JsonSerializer.Deserialize<ScheduledUrl>(message);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize message as ScheduledUrl: {Message}", message);
            return;
        }

        if (scheduledUrl == null)
        {
            _logger.LogWarning("Failed to deserialize message as ScheduledUrl: {Message}", message);
            return;
        }

        _logger.LogInformation(
            "Parsed ScheduledUrl - ID: {Id}, URL: {Url}, Priority: {Priority}",
            scheduledUrl.Id, scheduledUrl.Url, scheduledUrl.Priority);

        var objectName = ObjectNameGenerator.GenerateHierarchical(scheduledUrl.Url);
        _logger.LogInformation("Generated object name: {ObjectName} for URL: {Url}", objectName, scheduledUrl.Url);

        var metadata = await _storageManager.UploadContent(
            scheduledUrl.Url, 
            _minioOptions.Value.BucketName, 
            objectName, 
            cancellationToken);

        await UpdateDiscoveredUrl(scheduledUrl.Id, metadata, objectName, cancellationToken);
    }

    private async Task UpdateDiscoveredUrl(
        int discoveredUrlId, 
        UrlContentMetadata metadata, 
        string objectName, 
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDiscoveredUrlRepository>();

        var discoveredUrl = await repository.GetById(discoveredUrlId, cancellationToken);
        if (discoveredUrl == null)
        {
            _logger.LogWarning("DiscoveredUrl with ID {Id} not found in database", discoveredUrlId);
            return;
        }
        
        var now = _timeProvider.GetUtcNow();
        discoveredUrl.LastCrawlAt = now.ToUniversalTime();
        discoveredUrl.NextCrawlAt = now.AddDays(1).ToUniversalTime();
        discoveredUrl.LastStatusCode = metadata.StatusCode;
        discoveredUrl.ContentType = metadata.ContentType;
        discoveredUrl.ContentLength = metadata.ContentLength;
        discoveredUrl.ObjectName = objectName;
        discoveredUrl.Status = UrlStatus.Idle;

        var updated = await repository.Update(discoveredUrl, cancellationToken);
        if (!updated)
        {
            _logger.LogWarning("Failed to update DiscoveredUrl {Id} with metadata", discoveredUrlId);
            return;
        }

        _logger.LogInformation(
            "Updated DiscoveredUrl {Id} with metadata - StatusCode: {StatusCode}, ContentType: {ContentType}, ContentLength: {ContentLength}",
            discoveredUrl.Id, metadata.StatusCode, metadata.ContentType, metadata.ContentLength);
    }
}
