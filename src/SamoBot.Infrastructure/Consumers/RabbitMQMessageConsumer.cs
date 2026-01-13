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
    private readonly RabbitMQOptions _options;
    private IConnection? _connection;
    private IModel? _channel;
    private EventingBasicConsumer? _consumer;

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    public RabbitMQMessageConsumer(ILogger<RabbitMQMessageConsumer> logger, IOptions<RabbitMQOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: _options.QueueName,
            durable: _options.Durable,
            exclusive: _options.Exclusive,
            autoDelete: _options.AutoDelete,
            arguments: null);

        _logger.LogInformation("RabbitMQ consumer connected. Queue: {QueueName}", _options.QueueName);

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
            queue: _options.QueueName,
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
