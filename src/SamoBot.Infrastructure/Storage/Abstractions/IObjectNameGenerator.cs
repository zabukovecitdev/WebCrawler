namespace SamoBot.Infrastructure.Storage.Abstractions;

public interface IObjectNameGenerator
{
    string GenerateHierarchical(string url, string? contentType = null);
}
