namespace SamoBot.Infrastructure.Storage.Services;

public interface IStorageManager
{
    Task CreateBucket(string bucketName);
}