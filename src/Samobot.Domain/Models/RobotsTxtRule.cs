namespace Samobot.Domain.Models;

public class RobotsTxtRule
{
    public string UserAgent { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public RuleType Type { get; set; }
}

public enum RuleType
{
    Allow,
    Disallow
}
