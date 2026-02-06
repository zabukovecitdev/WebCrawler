namespace SamoBot.Infrastructure.Models;

public class UrlContentMetadata
{
    public string? ContentType { get; set; }
    public long? ContentLength { get; set; }
    public int StatusCode { get; set; } = -1;
    public bool WasDeferred { get; set; }
    public bool WasBlocked { get; set; }
}