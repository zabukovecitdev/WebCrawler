using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SamoBot.Abstractions;

namespace SamoBot.Workers;

public class MessageConsumerWorker : BackgroundService
{
    private readonly ILogger<MessageConsumerWorker> _logger;
    private readonly IMessageConsumer _messageConsumer;
    private readonly IUrlCleanerService _urlCleanerService;

    public MessageConsumerWorker(
        ILogger<MessageConsumerWorker> logger,
        IMessageConsumer messageConsumer,
        IUrlCleanerService urlCleanerService)
    {
        _logger = logger;
        _messageConsumer = messageConsumer;
        _urlCleanerService = urlCleanerService;
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
                "Received message from {Source}. CreatedAt: {CreatedAt}, RoutingKey: {RoutingKey}, Message: {Message}",
                sender?.GetType().Name ?? "Unknown",
                e.CreatedAt,
                e.RoutingKey ?? "N/A",
                e.Message);

            await ProcessMessageAsync(e.Message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {Message}", e.Message);
        }
    }

    private async Task ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var parsed = _urlCleanerService.Clean(message);

        if (parsed.IsFailed)
        {
            _logger.LogWarning("Message is not a valid URL: {Message}", message);
            
            return;
        }
        
        _logger.LogInformation("Processing URL: {Url}", parsed.Value);

        await Task.CompletedTask;
    }

    public override void Dispose()
    {
        _messageConsumer.Dispose();
        base.Dispose();
    }
}
