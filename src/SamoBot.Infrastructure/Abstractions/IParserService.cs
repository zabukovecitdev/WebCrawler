namespace SamoBot.Infrastructure.Abstractions;

public interface IParserService
{
    Task ProcessUnparsedFetches(CancellationToken cancellationToken);
}
