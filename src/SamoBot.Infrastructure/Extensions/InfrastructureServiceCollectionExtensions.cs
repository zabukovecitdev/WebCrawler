using System.Data;
using System.Text.Json;
using Dapper;
using Meilisearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Neo4j.Driver;
using Polly;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Cache;
using SamoBot.Infrastructure.Data;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Database;
using SamoBot.Infrastructure.Graph;
using SamoBot.Infrastructure.Graph.Abstractions;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Parsers;
using SamoBot.Infrastructure.Policies;
using SamoBot.Infrastructure.Producers;
using SamoBot.Infrastructure.Services;
using SamoBot.Infrastructure.Services.Abstractions;
using SamoBot.Infrastructure.Storage.Abstractions;
using SamoBot.Infrastructure.Storage.Services;
using SamoBot.Infrastructure.Validators;
using SqlKata.Compilers;
using SqlKata.Execution;
using StackExchange.Redis;

namespace SamoBot.Infrastructure.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        SqlMapper.AddTypeHandler<JsonElement>(new DapperJsonElementTypeHandler());

        services.Configure<DatabaseOptions>(
            configuration.GetSection(DatabaseOptions.SectionName));

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

        services.AddScoped<IDiscoveredUrlRepository, DiscoveredUrlRepository>();
        services.AddScoped<IUrlFetchRepository, UrlFetchRepository>();
        services.AddScoped<IParsedDocumentRepository, ParsedDocumentRepository>();
        services.AddScoped<IIndexJobRepository, IndexJobRepository>();
        services.AddScoped<IIndexJobService, IndexJobService>();
        services.AddScoped<ICrawlJobRepository, CrawlJobRepository>();
        services.AddScoped<ICrawlJobEventRepository, CrawlJobEventRepository>();

        return services;
    }

    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisOptions>(
            configuration.GetSection(RedisOptions.SectionName));

        services.AddSingleton<IConnectionMultiplexer?>(sp =>
        {
            try
            {
                var options = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("Redis");

                var redisConfig = ConfigurationOptions.Parse(options.ConnectionString);
                redisConfig.DefaultDatabase = options.Database;
                redisConfig.AbortOnConnectFail = false;
                redisConfig.ConnectRetry = 3;
                redisConfig.ConnectTimeout = 5000;

                var multiplexer = ConnectionMultiplexer.Connect(redisConfig);
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

        return services;
    }

    public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MessageBrokerOptions>(
            configuration.GetSection(MessageBrokerOptions.SectionName));
        services.Configure<RabbitMQConnectionOptions>(
            configuration.GetSection(RabbitMQConnectionOptions.SectionName));
        services.Configure<DiscoveredUrlQueueOptions>(
            configuration.GetSection(DiscoveredUrlQueueOptions.SectionName));
        services.Configure<ScheduledUrlQueueOptions>(
            configuration.GetSection(ScheduledUrlQueueOptions.SectionName));

        services.AddSingleton<IUrlScheduler, UrlScheduler>();
        services.AddSingleton<IDiscoveredUrlPublisher, DiscoveredUrlPublisher>();

        return services;
    }

    public static IServiceCollection AddMinioStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MinioOptions>(
            configuration.GetSection(MinioOptions.SectionName));

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

        services.AddScoped<IMinioHtmlUploader, MinioHtmlUploader>();
        services.AddScoped<IObjectNameGenerator, ObjectNameGenerator>();
        services.AddScoped<Storage.Abstractions.IStorageManager, MinioStorageManager>();

        return services;
    }

    public static IServiceCollection AddCrawling(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CrawlerOptions>(
            configuration.GetSection(CrawlerOptions.SectionName));

        // Robots.txt
        services.AddSingleton<IRobotsTxtParser, Parsers.RobotsTxtParser>();
        services.AddScoped<IRobotsTxtRepository, RobotsTxtRepository>();
        services.AddScoped<IRobotsTxtService, RobotsTxtService>();

        // Validators
        services.AddSingleton<IMetaRobotsValidator, MetaRobotsValidator>();
        services.AddSingleton<IHtmlContentValidator, HtmlContentValidator>();

        // Crawl policies
        services.AddScoped<RobotsTxtPolicy>();
        services.AddScoped<PolitenessPolicy>();
        services.AddScoped<ICrawlPolicy>(sp => new PolicyChain(new ICrawlPolicy[]
        {
            sp.GetRequiredService<RobotsTxtPolicy>(),
            sp.GetRequiredService<PolitenessPolicy>()
        }));

        // Fetch & pipeline
        services.AddSingleton<IUrlFetchService, UrlFetchService>();
        services.AddScoped<IFetchRecordPersistenceService, FetchRecordPersistenceService>();
        services.AddScoped<IContentProcessingPipeline, ContentProcessingPipeline>();
        services.AddScoped<Storage.Abstractions.IHtmlParser, Storage.Services.HtmlParser>();

        // Retry policy
        services.AddSingleton<IAsyncPolicy<HttpResponseMessage>>(sp =>
        {
            var crawlerOptions = sp.GetRequiredService<IOptions<CrawlerOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<MinioStorageManager>>();
            return CrawlerRetryPolicyBuilder.BuildRetryPolicy(crawlerOptions, logger);
        });

        // HTTP clients
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

        services.AddCrawlControlPlane(configuration);

        return services;
    }

    public static IServiceCollection AddCrawlControlPlane(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CrawlTelemetryOptions>(
            configuration.GetSection(CrawlTelemetryOptions.SectionName));
        services.Configure<ChromeRenderingOptions>(
            configuration.GetSection(ChromeRenderingOptions.SectionName));
        services.AddScoped<ICrawlTelemetryService, CrawlTelemetryService>();
        services.AddSingleton<IJsRenderService, JsRenderService>();
        services.AddScoped<ICrawlJobService, CrawlJobService>();

        return services;
    }

    public static IServiceCollection AddMeilisearch(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MeilisearchOptions>(
            configuration.GetSection(MeilisearchOptions.SectionName));

        services.AddSingleton<MeilisearchClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MeilisearchOptions>>().Value;
            return new MeilisearchClient(options.Host, options.ApiKey);
        });

        services.AddScoped<IIndexerService, IndexService>();
        services.AddScoped<ISearchService, SearchService>();

        return services;
    }

    public static IServiceCollection AddNeo4j(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Neo4jOptions>(
            configuration.GetSection(Neo4jOptions.SectionName));

        services.AddSingleton<IDriver?>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<Neo4jOptions>>().Value;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Neo4j");
            if (!opts.Enabled) { logger.LogWarning("Neo4j disabled."); return null; }
            try
            {
                return GraphDatabase.Driver(opts.Uri, AuthTokens.Basic(opts.Username, opts.Password));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create Neo4j driver. Graph writes skipped.");
                return null;
            }
        });

        services.AddScoped<IPageGraphRepository>(sp =>
        {
            var driver = sp.GetService<IDriver?>();
            return driver is null
                ? new NullPageGraphRepository()
                : new PageGraphRepository(driver, sp.GetRequiredService<ILogger<PageGraphRepository>>());
        });

        services.AddHostedService<GraphConstraintInitializer>();

        return services;
    }

    /// <summary>
    /// Registers all infrastructure services. Prefer using the individual Add* methods
    /// to only register what each service actually needs.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase(configuration);
        services.AddRedisCache(configuration);
        services.AddRabbitMq(configuration);
        services.AddMinioStorage(configuration);
        services.AddCrawling(configuration);
        services.AddMeilisearch(configuration);
        services.AddNeo4j(configuration);

        return services;
    }
}
