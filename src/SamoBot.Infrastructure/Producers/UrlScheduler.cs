using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Options;

namespace SamoBot.Infrastructure.Producers;

public class UrlScheduler : IUrlScheduler, IDisposable
{
    private readonly ILogger<UrlScheduler> _logger;
    private readonly RabbitMQConnectionOptions _connectionOptions;
    private readonly ScheduledUrlQueueOptions _queueOptions;
    private readonly TimeProvider _timeProvider;
    private IConnection? _connection;
    private IModel? _channel;
    private bool _initialized = false;

    public UrlScheduler(
        ILogger<UrlScheduler> logger,
        IOptions<RabbitMQConnectionOptions> connectionOptions,
        IOptions<ScheduledUrlQueueOptions> queueOptions,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _connectionOptions = connectionOptions.Value;
        _queueOptions = queueOptions.Value;
        _timeProvider = timeProvider;
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

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

        _channel.ExchangeDeclare(
            exchange: _queueOptions.ExchangeName,
            type: _queueOptions.ExchangeType,
            durable: _queueOptions.Durable,
            autoDelete: false,
            arguments: null);

        _channel.QueueDeclare(
            queue: _queueOptions.QueueName,
            durable: _queueOptions.Durable,
            exclusive: _queueOptions.Exclusive,
            autoDelete: _queueOptions.AutoDelete,
            arguments: null);

        _channel.QueueBind(
            queue: _queueOptions.QueueName,
            exchange: _queueOptions.ExchangeName,
            routingKey: _queueOptions.RoutingKey);

        _initialized = true;

        _logger.LogInformation(
            "RabbitMQ producer initialized. Exchange: {ExchangeName} ({ExchangeType}), Queue: {QueueName}, RoutingKey: {RoutingKey}",
            _queueOptions.ExchangeName, _queueOptions.ExchangeType, _queueOptions.QueueName, _queueOptions.RoutingKey);
    }

    public Task Publish(IEnumerable<DiscoveredUrl> urls, CancellationToken cancellationToken = default)
    {
        return PublishBatch(urls, cancellationToken);
    }

    public Task PublishBatch(IEnumerable<DiscoveredUrl> urls, CancellationToken cancellationToken = default)
    {
        Initialize();

        var urlList = urls.ToList();
        if (urlList.Count == 0)
        {
            return Task.CompletedTask;
        }

        var published = 0;
        var unixTimestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        
        foreach (var url in urlList)
        {
            try
            {
                var json = JsonSerializer.Serialize(url);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = _channel!.CreateBasicProperties();
                properties.Headers = new Dictionary<string, object>
                {
                    { "version", unixTimestamp }
                };

                _channel.BasicPublish(
                    exchange: _queueOptions.ExchangeName,
                    routingKey: _queueOptions.RoutingKey,
                    basicProperties: properties,
                    body: body);

                published++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish URL {Url} (ID: {Id})", url.Url, url.Id);
                throw;
            }
        }

        _logger.LogDebug("Published {Count} URLs to exchange '{ExchangeName}' with routing key '{RoutingKey}'",
            published, _queueOptions.ExchangeName, _queueOptions.RoutingKey);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
