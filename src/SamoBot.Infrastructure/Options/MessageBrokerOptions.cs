namespace SamoBot.Infrastructure.Options;

public class MessageBrokerOptions
{
    public const string SectionName = "MessageBroker";

    public string Provider { get; set; } = "Kafka"; // "Kafka" or "RabbitMQ"
}
