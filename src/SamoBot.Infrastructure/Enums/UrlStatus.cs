using System.Text.Json.Serialization;

namespace Samobot.Infrastructure.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UrlStatus
{
    None = 0,
    Idle = 1,
    InFlight = 2,
    Disabled = 3
}