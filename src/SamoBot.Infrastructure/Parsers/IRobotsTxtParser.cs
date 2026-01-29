using FluentResults;
using RobotsTxtModel = Samobot.Domain.Models.RobotsTxt;

namespace SamoBot.Infrastructure.Parsers;

public interface IRobotsTxtParser
{
    Result<RobotsTxtModel> Parse(string content, string host, DateTime fetchedAt, TimeSpan? cacheTtl = null);
    bool IsUrlAllowed(RobotsTxtModel robotsTxt, string url, string userAgent);
}
