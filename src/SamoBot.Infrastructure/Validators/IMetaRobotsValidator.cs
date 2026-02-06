using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Validators;

public interface IMetaRobotsValidator
{
    /// <summary>
    /// Checks if links should be followed based on meta robots directives.
    /// </summary>
    /// <param name="directives">Robots directives from parsed HTML</param>
    /// <returns>True if links should be followed, false otherwise</returns>
    bool ShouldFollowLinks(RobotsDirectives? directives);

    /// <summary>
    /// Checks if the page should be indexed based on meta robots directives.
    /// </summary>
    /// <param name="directives">Robots directives from parsed HTML</param>
    /// <returns>True if page should be indexed, false otherwise</returns>
    bool ShouldIndexPage(RobotsDirectives? directives);
}
