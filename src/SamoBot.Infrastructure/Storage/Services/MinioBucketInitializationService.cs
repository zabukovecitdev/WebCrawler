using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SamoBot.Infrastructure.Options;

namespace SamoBot.Infrastructure.Storage.Services;

public class MinioBucketInitializationService : IHostedService
{
    private readonly IStorageManager _storageManager;
    private readonly MinioOptions _minioOptions;
    private readonly ILogger<MinioBucketInitializationService> _logger;

    public MinioBucketInitializationService(
        IStorageManager storageManager,
        IOptions<MinioOptions> minioOptions,
        ILogger<MinioBucketInitializationService> logger)
    {
        _storageManager = storageManager;
        _minioOptions = minioOptions.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing MinIO bucket: {BucketName}", _minioOptions.BucketName);
            await _storageManager.CreateBucket(_minioOptions.BucketName);
            _logger.LogInformation("MinIO bucket initialization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MinIO bucket: {BucketName}", _minioOptions.BucketName);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
