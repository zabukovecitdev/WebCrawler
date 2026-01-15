namespace SamoBot.Infrastructure.Options;

public class DiscoveredUrlQueueOptions
{
    public const string SectionName = "RabbitMQ:DiscoveredUrlQueue";

    public string ExchangeName { get; set; } = "cs";
    public string ExchangeType { get; set; } = "topic";
    public string QueueName { get; set; } = "discovered.urls";
    public string RoutingKey { get; set; } = "url.discovered";
    public bool Durable { get; set; } = true;
    public bool Exclusive { get; set; } = false;
    public bool AutoDelete { get; set; } = false;
}
