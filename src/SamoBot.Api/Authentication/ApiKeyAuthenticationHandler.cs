using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SamoBot.Api.Authentication;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private const string HeaderName = "X-Api-Key";

    private readonly IOptionsMonitor<ApiKeyOptions> _apiKeyOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<ApiKeyOptions> apiKeyOptions)
        : base(options, logger, encoder)
    {
        _apiKeyOptions = apiKeyOptions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var keyHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expected = _apiKeyOptions.CurrentValue.Key;
        if (string.IsNullOrEmpty(expected))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key not configured on server."));
        }

        if (keyHeader != expected)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "api-key-user"),
            new Claim(ClaimTypes.Name, "Dashboard")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class ApiKeyOptions
{
    public const string SectionName = "DashboardApi";

    public string Key { get; set; } = string.Empty;
}
