namespace SamoBot;

public interface IMessageConsumer : IDisposable
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    event EventHandler<MessageReceivedEventArgs>? MessageReceived;
}

public class MessageReceivedEventArgs : EventArgs
{
    public string Message { get; init; } = string.Empty;
    public string? Topic { get; init; }
    public string? RoutingKey { get; init; }
}
