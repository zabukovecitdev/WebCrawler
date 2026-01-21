using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace SamoBot.Infrastructure.Storage.Services;

public class MinioStorageManager : IStorageManager
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioStorageManager> _logger;

    public MinioStorageManager(
        IMinioClient minioClient,
        ILogger<MinioStorageManager> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
    }

    public async Task CreateBucket(string bucketName)
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(bucketName);

            var found = await _minioClient.BucketExistsAsync(bucketExistsArgs);
            if (found)
            {
                _logger.LogInformation("Bucket {BucketName} already exists", bucketName);
            }
            else
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(bucketName);

                await _minioClient.MakeBucketAsync(makeBucketArgs);
                _logger.LogInformation("Bucket {BucketName} created successfully", bucketName);
            }
        }
        catch (MinioException e)
        {
            _logger.LogError(e, "Error occurred while creating bucket {BucketName}", bucketName);
            throw;
        }
    }

}
