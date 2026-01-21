namespace SamoBot.Infrastructure.Storage.Abstractions;

public interface IHtmlContentValidator
{
    bool IsHtml(string? contentType, byte[] contentBytes);
}
