namespace SamoBot.Infrastructure.Storage.Services;

public interface IContentUploadBuilderFactory
{
    ContentUploadBuilder Create(ContentUploadContext context);
}
