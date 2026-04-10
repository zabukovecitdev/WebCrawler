using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SamoBot.Infrastructure.Models;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Storage.Abstractions;
using Microsoft.Playwright;

namespace SamoBot.Infrastructure.Storage.Services;

public class JsRenderService : IJsRenderService, IAsyncDisposable
{
    private readonly ChromeRenderingOptions _options;
    private readonly ILogger<JsRenderService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public JsRenderService(IOptions<ChromeRenderingOptions> options, ILogger<JsRenderService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FetchedContent?> RenderPageAsync(string url, int timeoutMs, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.CdpEndpoint))
        {
            _logger.LogDebug("JS rendering disabled or no CDP endpoint configured");
            return null;
        }

        try
        {
            await EnsureBrowserAsync(cancellationToken);
            if (_browser == null)
            {
                return null;
            }

            var page = await _browser.NewPageAsync();
            try
            {
                var sw = Stopwatch.StartNew();
                var response = await page.GotoAsync(url, new PageGotoOptions
                {
                    Timeout = timeoutMs,
                    WaitUntil = WaitUntilState.NetworkIdle
                });

                var status = (int)(response?.Status ?? 0);
                var html = await page.ContentAsync();
                sw.Stop();

                var bytes = Encoding.UTF8.GetBytes(html);
                return new FetchedContent
                {
                    StatusCode = status,
                    ContentType = "text/html; charset=utf-8",
                    ContentLength = bytes.Length,
                    ContentBytes = bytes,
                    ResponseTimeMs = sw.ElapsedMilliseconds
                };
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JS render failed for {Url}", url);
            return null;
        }
    }

    private async Task EnsureBrowserAsync(CancellationToken cancellationToken)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_browser != null)
            {
                return;
            }

            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.ConnectOverCDPAsync(_options.CdpEndpoint);
            _logger.LogInformation("Connected Playwright to CDP at {Endpoint}", _options.CdpEndpoint);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
    }
}
