namespace SamoBot.Abstractions;

public interface IMessageConsumer : IDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    event EventHandler<MessageReceivedEventArgs>? MessageReceived;
}

public class MessageReceivedEventArgs : EventArgs
{
    public string Message { get; init; } = string.Empty;
    public string? RoutingKey { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
