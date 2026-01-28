namespace SamoBot.Parser.Services;

public interface IParserService
{
    Task ProcessUnparsedFetches(CancellationToken cancellationToken);
}
