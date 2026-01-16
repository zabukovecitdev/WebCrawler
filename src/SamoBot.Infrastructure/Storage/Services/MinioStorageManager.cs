using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace SamoBot.Infrastructure.Storage.Services;

public class MinioStorageManager(IMinioClient minioClient, ILogger<MinioStorageManager> logger)
    : IStorageManager
{
    public async Task CreateBucket(string bucketName)
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(bucketName);
            
            var found = await minioClient.BucketExistsAsync(bucketExistsArgs);
            if (found)
            {
                logger.LogInformation("Bucket {BucketName} already exists", bucketName);
            }
            else
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(bucketName);
                
                await minioClient.MakeBucketAsync(makeBucketArgs);
                logger.LogInformation("Bucket {BucketName} created successfully", bucketName);
            }
        }
        catch (MinioException e)
        {
            logger.LogError(e, "Error occurred while creating bucket {BucketName}", bucketName);
            throw;
        }
    }
}