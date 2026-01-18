using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Data;
using SamoBot.Infrastructure.Database;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Producers;
using SamoBot.Infrastructure.Storage.Services;
using SqlKata.Compilers;
using SqlKata.Execution;

namespace SamoBot.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MessageBrokerOptions>(
            configuration.GetSection(MessageBrokerOptions.SectionName));
        services.Configure<RabbitMQConnectionOptions>(
            configuration.GetSection(RabbitMQConnectionOptions.SectionName));
        services.Configure<DiscoveredUrlQueueOptions>(
            configuration.GetSection(DiscoveredUrlQueueOptions.SectionName));
        services.Configure<ScheduledUrlQueueOptions>(
            configuration.GetSection(ScheduledUrlQueueOptions.SectionName));
        services.Configure<DatabaseOptions>(
            configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<MinioOptions>(
            configuration.GetSection(MinioOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddScoped<IDbConnection>(sp =>
        {
            var factory = sp.GetRequiredService<IDbConnectionFactory>();
            var connection = factory.CreateConnection();
            connection.Open();
            return connection;
        });

        services.AddScoped<QueryFactory>(sp =>
        {
            var connection = sp.GetRequiredService<IDbConnection>();
            var compiler = new PostgresCompiler();
            return new QueryFactory(connection, compiler);
        });

        services.AddSingleton<IUrlScheduler, UrlScheduler>();
        services.AddScoped<IDiscoveredUrlRepository, DiscoveredUrlRepository>();

        services.AddScoped<IMinioClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
            var endpoint = $"{options.Endpoint}:{options.Port}";
            
            var client = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey);
            
            if (options.UseSsl)
            {
                client = client.WithSSL();
            }
            
            if (!string.IsNullOrEmpty(options.Region))
            {
                client = client.WithRegion(options.Region);
            }
            
            return client.Build();
        });

        services.AddScoped<IStorageManager, MinioStorageManager>();
        services.AddHttpClient();
        services.AddHostedService<MinioBucketInitializationService>();

        return services;
    }
}
