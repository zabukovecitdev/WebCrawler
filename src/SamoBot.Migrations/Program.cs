using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration["Database:ConnectionString"]
    ?? throw new InvalidOperationException("Database connection string is not configured");

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services
    .AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole());

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Starting database migrations...");

    await EnsureDatabaseExistsAsync(connectionString, logger);

    using var scope = host.Services.CreateScope();
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    
    runner.MigrateUp();
    
    logger.LogInformation("Database migrations completed successfully.");
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred while running migrations.");
    
    Environment.Exit(1);
}

static async Task EnsureDatabaseExistsAsync(string connectionString, ILogger logger)
{
    var builder = new NpgsqlConnectionStringBuilder(connectionString);
    var databaseName = builder.Database;
    builder.Database = "postgres"; // Connect to default database

    await using var connection = new NpgsqlConnection(builder.ConnectionString);
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = "SELECT 1 FROM pg_database WHERE datname = @databaseName";
    command.Parameters.AddWithValue("databaseName", databaseName ?? string.Empty);

    var exists = await command.ExecuteScalarAsync() != null;

    if (!exists)
    {
        logger.LogInformation("Database '{DatabaseName}' does not exist. Creating...", databaseName);
        var escapedDatabaseName = databaseName?.Replace("\"", "\"\"");
        command.CommandText = $@"CREATE DATABASE ""{escapedDatabaseName}""";
        command.Parameters.Clear();
        
        await command.ExecuteNonQueryAsync();
        logger.LogInformation("Database '{DatabaseName}' created successfully.", databaseName);
    }
    else
    {
        logger.LogInformation("Database '{DatabaseName}' already exists.", databaseName);
    }
}
