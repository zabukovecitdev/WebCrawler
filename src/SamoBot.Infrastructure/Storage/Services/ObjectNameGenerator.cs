using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace SamoBot.Infrastructure.Storage.Services;

public class ObjectNameGenerator : IObjectNameGenerator
{
    private static readonly Regex InvalidCharsRegex = new(@"[^a-zA-Z0-9\-_./]", RegexOptions.Compiled);
    private static readonly Regex MultipleSlashesRegex = new(@"/{2,}", RegexOptions.Compiled);
    
    public string GenerateHierarchical(string url, string? contentType = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return GenerateHashBased(url, contentType);
        }

        var domain = SanitizeDomain(uri.Host);
        var path = SanitizePath(uri.PathAndQuery);
        var hash = GenerateUrlHash(url);
        var extension = GetExtensionFromContentType(contentType) ?? GetExtensionFromPath(uri) ?? "html";

        var objectName = $"{domain}/{path}/{hash}.{extension}";
        
        objectName = MultipleSlashesRegex.Replace(objectName, "/");
        
        return objectName;
    }

    private static string GenerateHashBased(string url, string? contentType = null)
    {
        var hash = GenerateUrlHash(url);
        var extension = GetExtensionFromContentType(contentType) ?? 
                       (Uri.TryCreate(url, UriKind.Absolute, out var uri) ? GetExtensionFromPath(uri) : null) ?? 
                       "html";
        
        return $"{hash}.{extension}";
    }

    private static string SanitizeDomain(string host)
    {
        var domain = host.Split(':')[0];
        
        return InvalidCharsRegex.Replace(domain, "_");
    }

    private static string SanitizePath(string pathAndQuery)
    {
        if (string.IsNullOrEmpty(pathAndQuery) || pathAndQuery == "/")
        {
            return "index";
        }

        var path = pathAndQuery.TrimStart('/');
        
        path = path.Split('?')[0].Split('#')[0];
        
        path = InvalidCharsRegex.Replace(path, "_");
        
        path = path.TrimEnd('/');
        
        if (string.IsNullOrEmpty(path))
        {
            return "index";
        }
        
        return path;
    }

    private static string GenerateUrlHash(string url)
    {
        var bytes = Encoding.UTF8.GetBytes(url);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? GetExtensionFromContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var mediaType = contentType.Split(';')[0].Trim().ToLowerInvariant();

        return mediaType switch
        {
            "text/html" => "html",
            "text/plain" => "txt",
            "application/json" => "json",
            "application/xml" or "text/xml" => "xml",
            "text/css" => "css",
            "text/javascript" or "application/javascript" => "js",
            "application/pdf" => "pdf",
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/gif" => "gif",
            "image/webp" => "webp",
            "image/svg+xml" => "svg",
            "application/rss+xml" => "rss",
            "application/atom+xml" => "atom",
            _ => null
        };
    }

    private static string? GetExtensionFromPath(Uri uri)
    {
        var path = uri.AbsolutePath;
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return null;
        }

        var lastDot = path.LastIndexOf('.');
        if (lastDot > 0 && lastDot < path.Length - 1)
        {
            var ext = path[(lastDot + 1)..].ToLowerInvariant();
            var validExtensions = new[] { "html", "htm", "xml", "json", "txt", "css", "js", "pdf", "jpg", "jpeg", "png", "gif", "webp", "svg" };
            if (validExtensions.Contains(ext))
            {
                return ext;
            }
        }

        return null;
    }
}
