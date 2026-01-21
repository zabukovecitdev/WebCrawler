using System.Text;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace SamoBot.Infrastructure.Storage.Services;

public class HtmlContentValidator : IHtmlContentValidator
{
    public bool IsHtml(string? contentType, byte[] contentBytes)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var mediaType = contentType.Split(';')[0].Trim().ToLowerInvariant();
            if (mediaType is "text/html" or "application/xhtml+xml")
            {
                return true;
            }
        }

        var snippetLength = Math.Min(contentBytes.Length, 4096);
        if (snippetLength <= 0)
        {
            return false;
        }

        var snippet = Encoding.UTF8.GetString(contentBytes, 0, snippetLength);
        return snippet.Contains("<!doctype html", StringComparison.OrdinalIgnoreCase)
               || snippet.Contains("<html", StringComparison.OrdinalIgnoreCase);
    }
}
