using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SamoBot;

public class MessageConsumerWorker : BackgroundService
{
    private readonly ILogger<MessageConsumerWorker> _logger;
    private readonly IMessageConsumer _messageConsumer;

    public MessageConsumerWorker(
        ILogger<MessageConsumerWorker> logger,
        IMessageConsumer messageConsumer)
    {
        _logger = logger;
        _messageConsumer = messageConsumer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _messageConsumer.MessageReceived += OnMessageReceived;
        
        await _messageConsumer.StartAsync(stoppingToken);
        _logger.LogInformation("Message consumer started.");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Message consumer worker cancellation requested.");
        }
        finally
        {
            _messageConsumer.MessageReceived -= OnMessageReceived;
            await _messageConsumer.StopAsync(stoppingToken);
            _logger.LogInformation("Message consumer worker stopped.");
        }
    }

    private async void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        try
        {
            _logger.LogInformation(
                "Received message from {Source}. Topic: {Topic}, RoutingKey: {RoutingKey}, Message: {Message}",
                sender?.GetType().Name ?? "Unknown",
                e.Topic ?? "N/A",
                e.RoutingKey ?? "N/A",
                e.Message);

            await ProcessMessageAsync(e.Message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {Message}", e.Message);
        }
    }

    private async Task ProcessMessageAsync(string message, CancellationToken cancellationToken)
    {
        var isUrl = Uri.TryCreate(message, UriKind.Absolute, out var uri);

        if (isUrl)
        {
            _logger.LogInformation("Processing URL: {Url}", uri);
        }
        await Task.CompletedTask;
    }

    public override void Dispose()
    {
        _messageConsumer.Dispose();
        base.Dispose();
    }
}
