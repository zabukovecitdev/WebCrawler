using Samobot.Domain.Enums;

namespace SamoBot.Infrastructure.Extensions;

public static class UrlStatusExtensions
{
    public static string AsString(this UrlStatus status)
    {
        return status switch
        {
            UrlStatus.None => "None",
            UrlStatus.Idle => "Idle",
            UrlStatus.InFlight => "InFlight",
            UrlStatus.Disabled => "Disabled",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown UrlStatus value")
        };
    }
}
