namespace SamoBot.Infrastructure.Storage.Abstractions;

public interface IStorageManager
{
    Task CreateBucket(string bucketName);
}
