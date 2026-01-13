using System.Web;
using FluentResults;

namespace SamoBot.Utilities;

public static class UrlNormalizer
{
    public static Result<string> Clean(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Result.Fail("Invalid URL");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var cleanedUri))
        {
            return Result.Fail("Invalid URL");
        }

        return Result.Ok(cleanedUri.Normalize());
    }

    public static bool TryClean(string url, out string? cleanedUrl)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            cleanedUrl = null;

            return false;
        }

        cleanedUrl = uri.Normalize();
        
        return true;
    }
    
    private static string Normalize(this Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
            Fragment = string.Empty
        };

        if ((builder.Scheme == "http" && builder.Port == 80) ||
            (builder.Scheme == "https" && builder.Port == 443))
        {
            builder.Port = -1;
        }

        var path = builder.Path;
        if (path.Length > 1 && path.EndsWith("/"))
        {
            builder.Path = path.TrimEnd('/');
        }

        if (!string.IsNullOrEmpty(builder.Query))
        {
            var queryParameters = HttpUtility.ParseQueryString(builder.Query);

            var keys = queryParameters.AllKeys
                .Where(key => key != null)
                .OrderBy(key => key, StringComparer.InvariantCultureIgnoreCase);

            var normalized = HttpUtility.ParseQueryString(string.Empty);
            foreach (var key in keys)
            {
                normalized[key] = queryParameters[key];
            }

            builder.Query = normalized.ToString();
        }

        return builder.Uri.AbsoluteUri;
    }
}
