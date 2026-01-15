using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Options;

namespace SamoBot.Infrastructure.Consumers;

public class RabbitMQMessageConsumer : IMessageConsumer
{
    private readonly ILogger<RabbitMQMessageConsumer> _logger;
    private readonly RabbitMQConnectionOptions _connectionOptions;
    private readonly DiscoveredUrlQueueOptions _queueOptions;
    private IConnection? _connection;
    private IModel? _channel;
    private EventingBasicConsumer? _consumer;

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    public RabbitMQMessageConsumer(
        ILogger<RabbitMQMessageConsumer> logger,
        IOptions<RabbitMQConnectionOptions> connectionOptions,
        IOptions<DiscoveredUrlQueueOptions> queueOptions)
    {
        _logger = logger;
        _connectionOptions = connectionOptions.Value;
        _queueOptions = queueOptions.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
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

        _logger.LogInformation(
            "RabbitMQ consumer connected. Exchange: {ExchangeName} ({ExchangeType}), Queue: {QueueName}, RoutingKey: {RoutingKey}",
            _queueOptions.ExchangeName, _queueOptions.ExchangeType, _queueOptions.QueueName, _queueOptions.RoutingKey);

        _consumer = new EventingBasicConsumer(_channel);
        _consumer.Received += (model, args) =>
        {
            var body = args.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = args.RoutingKey;

            _logger.LogDebug("Received message from RabbitMQ. RoutingKey: {RoutingKey}, Message: {Message}",
                routingKey, message);

            MessageReceived?.Invoke(this, new MessageReceivedEventArgs
            {
                Message = message,
                RoutingKey = routingKey
            });

            _channel.BasicAck(args.DeliveryTag, multiple: false);
        };

        _channel.BasicConsume(
            queue: _queueOptions.QueueName,
            autoAck: false,
            consumer: _consumer
            );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _channel?.Close();
        _connection?.Close();
        
        _logger.LogInformation("RabbitMQ consumer stopped");
        
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
