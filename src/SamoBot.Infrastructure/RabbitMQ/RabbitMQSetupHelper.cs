using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SamoBot.Infrastructure.Options;

namespace SamoBot.Infrastructure.RabbitMQ;

public static class RabbitMQSetupHelper
{
    public static void DeclareExchangeAndQueue(
        IModel channel,
        DiscoveredUrlQueueOptions queueOptions,
        ILogger? logger = null)
    {
        channel.ExchangeDeclare(
            exchange: queueOptions.ExchangeName,
            type: queueOptions.ExchangeType,
            durable: queueOptions.Durable,
            autoDelete: false,
            arguments: null);

        channel.QueueDeclare(
            queue: queueOptions.QueueName,
            durable: queueOptions.Durable,
            exclusive: queueOptions.Exclusive,
            autoDelete: queueOptions.AutoDelete,
            arguments: null);

        channel.QueueBind(
            queue: queueOptions.QueueName,
            exchange: queueOptions.ExchangeName,
            routingKey: queueOptions.RoutingKey);

        logger?.LogDebug(
            "Declared exchange and queue. Exchange: {ExchangeName} ({ExchangeType}), Queue: {QueueName}, RoutingKey: {RoutingKey}",
            queueOptions.ExchangeName, queueOptions.ExchangeType, queueOptions.QueueName, queueOptions.RoutingKey);
    }

    public static void DeclareExchangeAndQueue(
        IModel channel,
        ScheduledUrlQueueOptions queueOptions,
        ILogger? logger = null)
    {
        channel.ExchangeDeclare(
            exchange: queueOptions.ExchangeName,
            type: queueOptions.ExchangeType,
            durable: queueOptions.Durable,
            autoDelete: false,
            arguments: null);

        channel.QueueDeclare(
            queue: queueOptions.QueueName,
            durable: queueOptions.Durable,
            exclusive: queueOptions.Exclusive,
            autoDelete: queueOptions.AutoDelete,
            arguments: null);

        channel.QueueBind(
            queue: queueOptions.QueueName,
            exchange: queueOptions.ExchangeName,
            routingKey: queueOptions.RoutingKey);

        logger?.LogDebug(
            "Declared exchange and queue. Exchange: {ExchangeName} ({ExchangeType}), Queue: {QueueName}, RoutingKey: {RoutingKey}",
            queueOptions.ExchangeName, queueOptions.ExchangeType, queueOptions.QueueName, queueOptions.RoutingKey);
    }
}
