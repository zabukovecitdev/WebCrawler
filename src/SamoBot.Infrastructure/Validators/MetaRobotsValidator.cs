using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Validators;

public class MetaRobotsValidator : IMetaRobotsValidator
{
    public bool ShouldFollowLinks(RobotsDirectives? directives)
    {
        if (directives == null)
            return true;

        // "none" blocks both indexing and following
        if (directives.None)
            return false;

        // Check for explicit nofollow
        return !directives.NoFollow;
    }

    public bool ShouldIndexPage(RobotsDirectives? directives)
    {
        if (directives == null)
            return true;

        // "none" blocks both indexing and following
        if (directives.None)
            return false;

        // Check for explicit noindex
        return !directives.NoIndex;
    }
}
