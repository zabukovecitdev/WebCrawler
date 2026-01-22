namespace Samobot.Domain.Models;

public class ScheduledUrl
{
    public int Id { get; set; }
    public string Host { get; set; }
    public string Url { get; set; } = string.Empty;
    public int Priority { get; set; }
}
