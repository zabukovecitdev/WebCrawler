using System.Text;
using Bogus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

// Find the SamoBot project directory (where appsettings.json is located)
// Try multiple possible locations
var possiblePaths = new[]
{
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "SamoBot"), // From bin/Debug/net10.0
    Path.Combine(Directory.GetCurrentDirectory(), "..", "SamoBot"), // From project root
    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "SamoBot"), // From src
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SamoBot")) // From bin absolute
};

string? samoBotProjectPath = null;
foreach (var path in possiblePaths)
{
    var fullPath = Path.GetFullPath(path);
    if (Directory.Exists(fullPath) && File.Exists(Path.Combine(fullPath, "appsettings.json")))
    {
        samoBotProjectPath = fullPath;
        break;
    }
}

if (samoBotProjectPath == null)
{
    throw new FileNotFoundException(
        $"Could not find appsettings.json. Searched in: {string.Join(", ", possiblePaths.Select(Path.GetFullPath))}");
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(samoBotProjectPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .AddDebug()
        .SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<Program>();

var hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
var port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
var userName = configuration["RabbitMQ:UserName"] ?? "guest";
var password = configuration["RabbitMQ:Password"] ?? "guest";
var virtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/";
var exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "url_discovery";
var exchangeType = configuration["RabbitMQ:ExchangeType"] ?? "topic";
var routingKey = configuration["RabbitMQ:RoutingKey"] ?? "url.discovered";

var count = args.Length > 0 && int.TryParse(args[0], out var parsedCount) ? parsedCount : 10;
var baseUrl = args.Length > 1 ? args[1] : null;

try
{
    logger.LogInformation("Connecting to RabbitMQ at {HostName}:{Port}", hostName, port);
    
    var factory = new ConnectionFactory
    {
        HostName = hostName,
        Port = port,
        UserName = userName,
        Password = password,
        VirtualHost = virtualHost
    };

    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();

    // Declare exchange
    channel.ExchangeDeclare(
        exchange: exchangeName,
        type: exchangeType,
        durable: true,
        autoDelete: false,
        arguments: null);

    logger.LogInformation("Publishing {Count} URLs to exchange '{ExchangeName}' with routing key '{RoutingKey}'", 
        count, exchangeName, routingKey);

    var faker = new Faker();
    var published = 0;

    for (int i = 0; i < count; i++)
    {
        var url = GenerateRealisticUrl(faker, baseUrl);
        var body = Encoding.UTF8.GetBytes(url);

        channel.BasicPublish(
            exchange: exchangeName,
            routingKey: routingKey,
            basicProperties: null,
            body: body);

        published++;
        
        if (published % 100 == 0)
        {
            logger.LogInformation("Published {Published} URLs so far...", published);
        }
    }

    logger.LogInformation("Successfully published {Published} URLs to exchange '{ExchangeName}' with routing key '{RoutingKey}'", 
        published, exchangeName, routingKey);
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred while publishing URLs");
    Environment.Exit(1);
}

static string GenerateRealisticUrl(Faker faker, string? baseUrl)
{
    // Generate scheme with potential uppercase (will be normalized to lowercase)
    var schemeBase = faker.PickRandom("http", "https");
    var scheme = faker.Random.Bool(0.3f) 
        ? schemeBase.ToUpperInvariant() 
        : schemeBase;
    
    string domain;
    string path;
    string query = "";
    string fragment = "";
    int? port = null;

    if (!string.IsNullOrEmpty(baseUrl))
    {
        // Use provided base URL
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            domain = baseUri.Host;
            scheme = baseUri.Scheme; // Use original scheme from base URL
        }
        else
        {
            domain = baseUrl.Replace("http://", "").Replace("https://", "").TrimEnd('/');
        }
    }
    else
    {
        // Generate realistic domain with potential uppercase (will be normalized to lowercase)
        var baseDomain = faker.Internet.DomainName();
        domain = faker.Random.Bool(0.3f)
            ? baseDomain.ToUpperInvariant()
            : faker.Random.Bool(0.2f)
                ? char.ToUpper(baseDomain[0]) + baseDomain.Substring(1)
                : baseDomain;
    }

    // Sometimes add default port (will be removed by normalizer)
    if (faker.Random.Bool(0.15f))
    {
        port = schemeBase == "http" ? 80 : 443;
    }
    // Sometimes add non-default port (will be kept)
    else if (faker.Random.Bool(0.1f))
    {
        port = faker.PickRandom(8080, 3000, 9000, 5000);
    }

    // Generate realistic path based on common URL patterns
    var pathType = faker.Random.WeightedRandom(
        new[] { "blog", "product", "article", "category", "user", "api", "static", "search", "root" },
        new[] { 0.15f, 0.15f, 0.15f, 0.12f, 0.08f, 0.10f, 0.10f, 0.05f, 0.10f }
    );

    path = pathType switch
    {
        "blog" => $"/blog/{faker.Lorem.Slug()}-{faker.Random.Int(2020, 2024)}",
        "product" => $"/products/{faker.Commerce.ProductName().ToLower().Replace(" ", "-")}",
        "article" => $"/articles/{faker.Lorem.Word()}/{faker.Lorem.Slug()}",
        "category" => $"/category/{faker.Commerce.Categories(1)[0].ToLower().Replace(" ", "-")}",
        "user" => $"/users/{faker.Internet.UserName()}",
        "api" => $"/api/v{faker.Random.Int(1, 3)}/{faker.PickRandom("users", "products", "orders", "posts")}",
        "static" => $"/{faker.PickRandom("about", "contact", "privacy", "terms", "help", "faq", "sitemap")}",
        "search" => "/search",
        "root" => faker.Random.Bool(0.5f) ? "/" : "",
        _ => ""
    };

    // Sometimes add trailing slash (kept as-is by normalizer)
    if (!string.IsNullOrEmpty(path) && path != "/" && faker.Random.Bool(0.2f))
    {
        path += "/";
    }

    // Add query parameters (40% chance) - intentionally unsorted to test normalization
    if (faker.Random.Bool(0.4f))
    {
        var queryParams = new List<(string key, string value)>();
        var paramCount = faker.Random.Int(1, 4);

        for (int i = 0; i < paramCount; i++)
        {
            var (key, value) = faker.PickRandom(
                ("zebra", faker.Random.Int(1, 100).ToString()),
                ("apple", faker.Random.Int(1, 100).ToString()),
                ("banana", faker.Random.Int(1, 100).ToString()),
                ("id", faker.Random.Int(1, 10000).ToString()),
                ("page", faker.Random.Int(1, 100).ToString()),
                ("limit", faker.PickRandom("10", "20", "50", "100")),
                ("offset", faker.Random.Int(0, 1000).ToString()),
                ("sort", faker.PickRandom("asc", "desc", "date", "name", "price", "popularity")),
                ("filter", faker.PickRandom("active", "published", "featured", "new", "sale")),
                ("category", faker.Commerce.Categories(1)[0].ToLower().Replace(" ", "-")),
                ("tag", faker.Lorem.Word()),
                ("q", faker.Lorem.Word()),
                ("search", faker.Lorem.Word()),
                ("ref", faker.Internet.DomainWord()),
                ("utm_source", faker.PickRandom("google", "facebook", "twitter", "email", "direct")),
                ("utm_medium", faker.PickRandom("cpc", "organic", "social", "email", "referral")),
                ("utm_campaign", faker.Company.CatchPhrase().ToLower().Replace(" ", "-"))
            );

            queryParams.Add((key, value));
        }

        // Intentionally shuffle to create unsorted query params (normalizer will sort them)
        queryParams = queryParams.OrderBy(_ => faker.Random.Int()).ToList();
        query = "?" + string.Join("&", queryParams.Select(p => $"{p.key}={Uri.EscapeDataString(p.value)}"));
    }

    // Add fragment (30% chance) - will be removed by normalizer
    if (faker.Random.Bool(0.3f))
    {
        fragment = "#" + faker.PickRandom("top", "section", "content", "main", faker.Lorem.Word());
    }

    var portStr = port.HasValue ? $":{port.Value}" : "";
    return $"{scheme}://{domain}{portStr}{path}{query}{fragment}";
}
