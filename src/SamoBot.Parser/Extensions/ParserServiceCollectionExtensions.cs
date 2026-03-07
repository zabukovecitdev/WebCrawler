using Microsoft.Extensions.DependencyInjection;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Parser.Services;

namespace SamoBot.Parser.Extensions;

public static class ParserServiceCollectionExtensions
{
    public static IServiceCollection AddParserServices(this IServiceCollection services)
    {
        services.AddScoped<IParserService, ParserService>();
        return services;
    }
}
