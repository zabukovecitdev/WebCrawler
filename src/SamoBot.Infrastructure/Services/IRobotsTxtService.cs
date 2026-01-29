using FluentResults;
using RobotsTxtModel = Samobot.Domain.Models.RobotsTxt;

namespace SamoBot.Infrastructure.Services;

public interface IRobotsTxtService
{
    Task<Result<bool>> IsUrlAllowed(string url, string userAgent, CancellationToken cancellationToken = default);
    Task<Result<int?>> GetCrawlDelayMs(string host, CancellationToken cancellationToken = default);
    Task<Result<RobotsTxtModel>> GetRobotsTxt(string host, CancellationToken cancellationToken = default);
}
