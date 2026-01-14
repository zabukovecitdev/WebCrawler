namespace SamoBot.Infrastructure.Options;

public class RabbitMQOptions
{
    public const string SectionName = "RabbitMQ";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "url_discovery";
    public string ExchangeType { get; set; } = "topic";
    public string QueueName { get; set; } = "urls_to_crawl";
    public string RoutingKey { get; set; } = "url.discovered";
    public bool Durable { get; set; } = true;
    public bool Exclusive { get; set; } = false;
    public bool AutoDelete { get; set; } = false;
}
