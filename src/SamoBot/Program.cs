using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SamoBot.Consumers;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Extensions;
using SamoBot.Services;
using SamoBot.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IMessageConsumer, UrlDiscoveryConsumer>();
builder.Services.AddScoped<ISchedulerService, SchedulerService>();
builder.Services.AddScoped<IParserService, ParserService>();

builder.Services.AddHostedService<MessageConsumerWorker>();
builder.Services.AddHostedService<CrawlerWorker>();
builder.Services.AddHostedService<DueQueueWorker>();
builder.Services.AddHostedService<RobotsTxtRefreshWorker>();
builder.Services.AddHostedService<SchedulerWorker>();
builder.Services.AddHostedService<ParserWorker>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var host = builder.Build();

await host.RunAsync();