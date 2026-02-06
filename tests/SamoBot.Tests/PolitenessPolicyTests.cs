using System.Text.Json;
using FluentAssertions;
using FluentResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Models;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Policies;
using SamoBot.Infrastructure.Services;

namespace SamoBot.Tests;

public class PolitenessPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_WhenClaimAllowed_ExecutesActionAndReturnsResult()
    {
        var cache = new FakeCache
        {
            ClaimResult = Result.Ok((true, 12345L))
        };
        var policy = CreatePolicy(cache, new CrawlerOptions
        {
            DefaultDelayMs = 1000,
            MinDelayMs = 500,
            MaxDelayMs = 60000
        });

        var scheduledUrl = new ScheduledUrl
        {
            Id = 42,
            Host = "example.com",
            Url = "https://example.com/page",
            Priority = 2
        };

        var executed = false;
        var result = await policy.ExecuteAsync(
            scheduledUrl,
            _ =>
            {
                executed = true;
                return Task.FromResult(Result.Ok(new UrlContentMetadata
                {
                    StatusCode = 200,
                    WasDeferred = false
                }));
            });

        result.IsSuccess.Should().BeTrue();
        result.Value.WasDeferred.Should().BeFalse();
        executed.Should().BeTrue();
        cache.Enqueued.Should().BeEmpty();
        cache.LastClaimArgs.Should().NotBeNull();
        cache.LastClaimArgs!.Value.DelayMs.Should().Be(1000);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClaimDenied_EnqueuesAndReturnsDeferred()
    {
        var dueTimestamp = 5000L;
        var cache = new FakeCache
        {
            ClaimResult = Result.Ok((false, dueTimestamp))
        };
        var policy = CreatePolicy(cache);

        var scheduledUrl = new ScheduledUrl
        {
            Id = 7,
            Host = "example.com",
            Url = "https://example.com/page",
            Priority = 1
        };

        var executed = false;
        var result = await policy.ExecuteAsync(
            scheduledUrl,
            _ =>
            {
                executed = true;
                return Task.FromResult(Result.Ok(new UrlContentMetadata()));
            });

        result.IsSuccess.Should().BeTrue();
        result.Value.WasDeferred.Should().BeTrue();
        executed.Should().BeFalse();
        cache.Enqueued.Should().ContainSingle();

        var (payload, timestamp) = cache.Enqueued.Single();
        timestamp.Should().Be(dueTimestamp);

        var deserialized = JsonSerializer.Deserialize<ScheduledUrl>(payload);
        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(scheduledUrl.Id);
        deserialized.Url.Should().Be(scheduledUrl.Url);
        deserialized.Host.Should().Be(scheduledUrl.Host);
        deserialized.Priority.Should().Be(scheduledUrl.Priority);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClaimDenied_EnqueueFails_ReturnsFailure()
    {
        var cache = new FakeCache
        {
            ClaimResult = Result.Ok((false, 5000L)),
            EnqueueResult = Result.Fail("enqueue failed")
        };
        var policy = CreatePolicy(cache);

        var executed = false;
        var result = await policy.ExecuteAsync(
            new ScheduledUrl { Url = "https://example.com", Host = "example.com" },
            _ =>
            {
                executed = true;
                return Task.FromResult(Result.Ok(new UrlContentMetadata()));
            });

        result.IsFailed.Should().BeTrue();
        executed.Should().BeFalse();
        cache.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenClaimFails_ReturnsFailure()
    {
        var cache = new FakeCache
        {
            ClaimResult = Result.Fail("redis down")
        };
        var policy = CreatePolicy(cache);

        var executed = false;
        var result = await policy.ExecuteAsync(
            new ScheduledUrl { Url = "https://example.com", Host = "example.com" },
            _ =>
            {
                executed = true;
                return Task.FromResult(Result.Ok(new UrlContentMetadata()));
            });

        result.IsFailed.Should().BeTrue();
        executed.Should().BeFalse();
        cache.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ClampsDelayToMinimum()
    {
        var cache = new FakeCache
        {
            ClaimResult = Result.Ok((true, 1L))
        };
        var policy = CreatePolicy(cache, new CrawlerOptions
        {
            DefaultDelayMs = 100,
            MinDelayMs = 500,
            MaxDelayMs = 60000
        });

        await policy.ExecuteAsync(
            new ScheduledUrl { Url = "https://example.com", Host = "example.com" },
            _ => Task.FromResult(Result.Ok(new UrlContentMetadata())));

        cache.LastClaimArgs.Should().NotBeNull();
        cache.LastClaimArgs!.Value.DelayMs.Should().Be(500);
    }

    [Fact]
    public async Task ExecuteAsync_ClampsDelayToMaximum()
    {
        var cache = new FakeCache
        {
            ClaimResult = Result.Ok((true, 1L))
        };
        var policy = CreatePolicy(cache, new CrawlerOptions
        {
            DefaultDelayMs = 120000,
            MinDelayMs = 500,
            MaxDelayMs = 60000
        });

        await policy.ExecuteAsync(
            new ScheduledUrl { Url = "https://example.com", Host = "example.com" },
            _ => Task.FromResult(Result.Ok(new UrlContentMetadata())));

        cache.LastClaimArgs.Should().NotBeNull();
        cache.LastClaimArgs!.Value.DelayMs.Should().Be(60000);
    }

    private static PolitenessPolicy CreatePolicy(FakeCache cache, CrawlerOptions? options = null)
    {
        var timeProvider = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        var crawlOptions = Options.Create(options ?? new CrawlerOptions());
        var fakeRobotsTxtService = new FakeRobotsTxtService();
        return new PolitenessPolicy(cache, timeProvider, crawlOptions, fakeRobotsTxtService, NullLogger<PolitenessPolicy>.Instance);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class FakeCache : ICache
    {
        public Result<(bool Allowed, long NextAllowedTimestamp)> ClaimResult { get; set; } =
            Result.Ok((true, 0L));

        public Result EnqueueResult { get; set; } = Result.Ok();

        public (string Key, long Now, long DelayMs)? LastClaimArgs { get; private set; }

        public List<(string Payload, long DueTimestamp)> Enqueued { get; } = new();

        public Task<Result<string?>> Get(string key, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> Set(string key, string value, TimeSpan? ttl = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<(bool Allowed, long NextAllowedTimestamp)>> TryClaimNextCrawl(
            string key,
            long currentTimestamp,
            long delayMs,
            CancellationToken cancellationToken = default)
        {
            LastClaimArgs = (key, currentTimestamp, delayMs);
            return Task.FromResult(ClaimResult);
        }

        public Task<Result<bool>> KeyDelete(string key, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<string?>> ZPopMin(string key, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> EnqueueUrlForCrawl(string url, long dueTimestamp, CancellationToken cancellationToken = default)
        {
            if (EnqueueResult.IsSuccess)
            {
                Enqueued.Add((url, dueTimestamp));
            }

            return Task.FromResult(EnqueueResult);
        }

        public Task<Result<IReadOnlyList<string>>> Dequeue(long currentTimestamp, int maxCount = 100,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeRobotsTxtService : IRobotsTxtService
    {
        public Task<Result<bool>> IsUrlAllowed(string url, string userAgent, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Ok(true));
        }

        public Task<Result<int?>> GetCrawlDelayMs(string host, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Ok<int?>(null));
        }

        public Task<Result<Infrastructure.Models.RobotsTxt>> GetRobotsTxt(string host, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
