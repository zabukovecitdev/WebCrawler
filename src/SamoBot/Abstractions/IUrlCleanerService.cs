using FluentResults;

namespace SamoBot.Abstractions;

public interface IUrlCleanerService
{
    public Result<Uri> Clean(string url);
}