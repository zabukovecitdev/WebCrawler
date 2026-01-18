namespace SamoBot.Infrastructure.Options;

public class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// Redis connection string (e.g., "localhost:6379" or "redis://localhost:6379")
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Redis database number (0-15, default is 0)
    /// </summary>
    public int Database { get; set; } = 0;
}
