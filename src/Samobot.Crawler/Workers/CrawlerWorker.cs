using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Samobot.Domain.Models;
using Samobot.Crawler.Utilities;
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
    private IConnection? _connection;
    private IModel? _channel;
    private EventingBasicConsumer? _consumer;
    private IStorageManager _storageManager;

    public CrawlerWorker(
        ILogger<CrawlerWorker> logger,
        IOptions<RabbitMQConnectionOptions> connectionOptions,
        IOptions<ScheduledUrlQueueOptions> queueOptions,
        IOptions<MinioOptions> minioOptions,
        IStorageManager storageManager)
    {
        _logger = logger;
        _minioOptions = minioOptions;
        _connectionOptions = connectionOptions.Value;
        _queueOptions = queueOptions.Value;
        _storageManager = storageManager;
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

        await _storageManager.UploadContent(
            scheduledUrl.Url, 
            _minioOptions.Value.BucketName, 
            objectName,
            scheduledUrl.Id,
            cancellationToken);
    }
}
