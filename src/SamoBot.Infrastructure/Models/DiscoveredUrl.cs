namespace SamoBot.Infrastructure.Models;

public class DiscoveredUrl
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? NormalizedUrl { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; }
}
