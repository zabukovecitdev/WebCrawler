namespace SamoBot.Infrastructure.Options;

public class ScheduledUrlQueueOptions
{
    public const string SectionName = "RabbitMQ:ScheduledUrlQueue";

    public string ExchangeName { get; set; } = "cs";
    public string ExchangeType { get; set; } = "topic";
    public string QueueName { get; set; } = "scheduled.urls";
    public string RoutingKey { get; set; } = "url.scheduled";
    public bool Durable { get; set; } = true;
    public bool Exclusive { get; set; } = false;
    public bool AutoDelete { get; set; } = false;
}
