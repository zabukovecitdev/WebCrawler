using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Models;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.RabbitMQ;

namespace SamoBot.Infrastructure.Producers;

public class DiscoveredUrlPublisher : IDiscoveredUrlPublisher, IDisposable
{
    private readonly ILogger<DiscoveredUrlPublisher> _logger;
    private readonly RabbitMQConnectionOptions _connectionOptions;
    private readonly DiscoveredUrlQueueOptions _queueOptions;
    private readonly TimeProvider _timeProvider;
    private IConnection? _connection;
    private IModel? _channel;
    private bool _initialized = false;

    public DiscoveredUrlPublisher(
        ILogger<DiscoveredUrlPublisher> logger,
        IOptions<RabbitMQConnectionOptions> connectionOptions,
        IOptions<DiscoveredUrlQueueOptions> queueOptions,
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

        RabbitMQSetupHelper.DeclareExchangeAndQueue(_channel, _queueOptions, _logger);

        _initialized = true;

        _logger.LogInformation(
            "DiscoveredUrlPublisher initialized. Exchange: {ExchangeName} ({ExchangeType}), Queue: {QueueName}, RoutingKey: {RoutingKey}",
            _queueOptions.ExchangeName, _queueOptions.ExchangeType, _queueOptions.QueueName, _queueOptions.RoutingKey);
    }

    public Task PublishUrlsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        var messages = urls.Select(u => new UrlDiscoveryMessage { Url = u });
        return PublishDiscoveriesAsync(messages, cancellationToken);
    }

    public Task PublishDiscoveriesAsync(IEnumerable<UrlDiscoveryMessage> discoveries, CancellationToken cancellationToken = default)
    {
        Initialize();

        var list = discoveries.ToList();
        if (list.Count == 0)
        {
            return Task.CompletedTask;
        }

        var published = 0;
        var unixTimestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();

        foreach (var msg in list)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));

                var properties = _channel!.CreateBasicProperties();
                properties.Headers = new Dictionary<string, object>
                {
                    { "version", unixTimestamp }
                };
                properties.ContentType = "application/json";

                _channel.BasicPublish(
                    exchange: _queueOptions.ExchangeName,
                    routingKey: _queueOptions.RoutingKey,
                    basicProperties: properties,
                    body: body);

                published++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish discovered URL: {Url}", msg.Url);
            }
        }

        _logger.LogDebug("Published {Count} discovered URLs to exchange '{ExchangeName}' with routing key '{RoutingKey}'",
            published, _queueOptions.ExchangeName, _queueOptions.RoutingKey);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
