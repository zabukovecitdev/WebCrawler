namespace SamoBot.Infrastructure.Services.Abstractions;

public interface ICrawlTelemetryService
{
    Task PublishAsync(int? crawlJobId, string eventType, object payload, CancellationToken cancellationToken = default);
}
