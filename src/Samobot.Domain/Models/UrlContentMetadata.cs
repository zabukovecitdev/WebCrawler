namespace Samobot.Domain.Models;

public class UrlContentMetadata
{
    public string ContentType { get; set; } = string.Empty;
    public long ContentLength { get; set; } = -1;
    public int StatusCode { get; set; } = -1;
    public bool WasDeferred { get; set; }
}