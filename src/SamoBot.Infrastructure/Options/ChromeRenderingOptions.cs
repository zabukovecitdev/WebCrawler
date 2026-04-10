namespace SamoBot.Infrastructure.Options;

public class ChromeRenderingOptions
{
    public const string SectionName = "ChromeRendering";

    /// <summary>When set, Playwright connects over CDP (e.g. http://chrome:9222).</summary>
    public string? CdpEndpoint { get; set; }

    public int DefaultTimeoutMs { get; set; } = 60_000;

    public bool Enabled { get; set; }
}
