using FluentResults;
using SamoBot.Abstractions;

namespace SamoBot.Services;

public class UrlCleanerService : IUrlCleanerService
{
    public Result<Uri> Clean(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Relative, out var cleanedUri))
        {
            return Result.Fail("Invalid URL");
        }
        
        return Result.Ok(RemoveFragment(cleanedUri));
    }
    
    private Uri RemoveFragment(Uri uri)
    {
        return new UriBuilder(uri) { Fragment = string.Empty }.Uri;
    }
}