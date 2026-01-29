using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Polly;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Cache;
using SamoBot.Infrastructure.Data;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Database;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Parsers;
using SamoBot.Infrastructure.Policies;
using SamoBot.Infrastructure.Producers;
using SamoBot.Infrastructure.Services;
using SamoBot.Infrastructure.Storage.Abstractions;
using SamoBot.Infrastructure.Storage.Services;
using SamoBot.Infrastructure.Validators;
using SqlKata.Compilers;
using SqlKata.Execution;
using StackExchange.Redis;

namespace SamoBot.Infrastructure.Extensions;

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
        services.Configure<CrawlerOptions>(
            configuration.GetSection(CrawlerOptions.SectionName));
        services.Configure<RedisOptions>(
            configuration.GetSection(RedisOptions.SectionName));

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

        // Redis connection - optional, application will work without it
        services.AddSingleton<IConnectionMultiplexer?>(sp =>
        {
            try
            {
                var options = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("Redis");
                
                var configuration = ConfigurationOptions.Parse(options.ConnectionString);
                configuration.DefaultDatabase = options.Database;
                configuration.AbortOnConnectFail = false; // Don't abort on connect failure - allows app to start
                configuration.ConnectRetry = 3; // Retry connection attempts
                configuration.ConnectTimeout = 5000; // 5 second timeout
                
                var multiplexer = ConnectionMultiplexer.Connect(configuration);
                
                // ConnectionMultiplexer.Connect() with AbortOnConnectFail=false won't throw,
                // but connection might not be ready immediately. DomainRateLimiter will check
                // IsConnected on each operation and fallback to in-memory if not connected.
                logger.LogInformation("Redis multiplexer created. Connection will be established asynchronously.");
                return multiplexer;
            }
            catch (Exception ex)
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("Redis");
                logger.LogWarning(ex, "Failed to create Redis connection. Application will continue without Redis, using in-memory rate limiting.");
                return null;
            }
        });

        services.AddScoped<ICache, RedisCache>();

        // Robots.txt services
        services.AddSingleton<IRobotsTxtParser, Parsers.RobotsTxtParser>();
        services.AddScoped<IRobotsTxtRepository, RobotsTxtRepository>();
        services.AddScoped<IRobotsTxtService, RobotsTxtService>();

        // Validators
        services.AddSingleton<IMetaRobotsValidator, MetaRobotsValidator>();

        // Policies - register individual policies and policy chain
        services.AddScoped<RobotsTxtPolicy>();
        services.AddScoped<PolitenessPolicy>();
        services.AddScoped<ICrawlPolicy>(sp => new PolicyChain(new ICrawlPolicy[]
        {
            sp.GetRequiredService<RobotsTxtPolicy>(),
            sp.GetRequiredService<PolitenessPolicy>()
        }));

        services.AddSingleton<IUrlScheduler, UrlScheduler>();
        services.AddSingleton<IDiscoveredUrlPublisher, DiscoveredUrlPublisher>();
        services.AddScoped<IDiscoveredUrlRepository, DiscoveredUrlRepository>();
        services.AddScoped<IUrlFetchRepository, UrlFetchRepository>();
        services.AddScoped<IParsedDocumentRepository, ParsedDocumentRepository>();
        services.AddScoped<IUrlFetchService, UrlFetchService>();
        services.AddSingleton<IHtmlContentValidator, HtmlContentValidator>();
        services.AddScoped<IMinioHtmlUploader, MinioHtmlUploader>();
        services.AddScoped<IObjectNameGenerator, ObjectNameGenerator>();
        services.AddScoped<IFetchRecordPersistenceService, FetchRecordPersistenceService>();
        services.AddScoped<IContentProcessingPipeline, ContentProcessingPipeline>();
        services.AddScoped<Storage.Abstractions.IHtmlParser, Storage.Services.HtmlParser>();

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
        
        services.AddSingleton<IAsyncPolicy<HttpResponseMessage>>(sp =>
        {
            var crawlerOptions = sp.GetRequiredService<IOptions<CrawlerOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<MinioStorageManager>>();
            return CrawlerRetryPolicyBuilder.BuildRetryPolicy(crawlerOptions, logger);
        });
        
        services.AddScoped<Storage.Abstractions.IStorageManager, MinioStorageManager>();
        
        services.AddHttpClient("crawl")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            })
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                
                client.DefaultRequestHeaders.Add("Accept", 
                    "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                
                client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                
                client.DefaultRequestHeaders.Add("DNT", "1");
                
                client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                
                client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
                client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
            });
        
        services.AddHttpClient();
        services.AddHostedService<MinioBucketInitializationService>();

        return services;
    }
}
