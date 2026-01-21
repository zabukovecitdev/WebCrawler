namespace Samobot.Domain.Models;

public class FetchedContent
{
    public int StatusCode { get; init; }
    public string? ContentType { get; init; }
    public byte[]? ContentBytes { get; init; }
    public long? ContentLength { get; init; }
    public string? Error { get; init; }
}
