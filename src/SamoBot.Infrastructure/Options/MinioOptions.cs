namespace SamoBot.Infrastructure.Options;

public class MinioOptions
{
    public const string SectionName = "MinIO";
    public string Endpoint { get; set; } = "localhost";
    public int Port { get; set; } = 9000;
    public bool UseSsl { get; set; } = false;
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string Region { get; set; } = string.Empty;
    public string BucketName { get; set; } = "samobot-content";
}
