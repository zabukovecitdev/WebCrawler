using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SamoBot;
using SamoBot.Abstractions;
using SamoBot.Consumers;
using SamoBot.Services;
using SamoBot.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<MessageBrokerOptions>(
    builder.Configuration.GetSection(MessageBrokerOptions.SectionName));

builder.Services.Configure<RabbitMQOptions>(
    builder.Configuration.GetSection(RabbitMQOptions.SectionName));

builder.Services.AddScoped<IUrlCleanerService, UrlCleanerService>();
builder.Services.AddSingleton<IMessageConsumer, RabbitMQMessageConsumer>();
builder.Services.AddHostedService<MessageConsumerWorker>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var host = builder.Build();

await host.RunAsync();