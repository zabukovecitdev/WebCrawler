using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SamoBot.Infrastructure.Cache;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Utilities;
using StackExchange.Redis;

namespace SamoBot.Tests;

public class RedisCachePolitenessTests
{
    private const int TestDatabase = 15;

    [Fact]
    public async Task TryClaimNextCrawl_WhenKeyMissing_AllowsAndSetsNext()
    {
        if (!TryCreateCache(out var cache, out var database, out var multiplexer))
        {
            return;
        }
        var key = $"test:next:{Guid.NewGuid():N}";
        try
        {
            const long now = 1000;
            const long delay = 500;

            var result = await cache.TryClaimNextCrawl(key, now, delay);

            result.IsSuccess.Should().BeTrue();
            result.Value.Allowed.Should().BeTrue();
            result.Value.NextAllowedTimestamp.Should().Be(now + delay);

            var stored = await database.StringGetAsync(key);
            stored.ToString().Should().Be((now + delay).ToString());
        }
        finally
        {
            await database.KeyDeleteAsync(key);
            multiplexer.Dispose();
        }
    }

    [Fact]
    public async Task TryClaimNextCrawl_WhenKeyInFuture_DisallowsAndKeepsTimestamp()
    {
        if (!TryCreateCache(out var cache, out var database, out var multiplexer))
        {
            return;
        }
        var key = $"test:next:{Guid.NewGuid():N}";
        try
        {
            const long now = 1000;
            const long delay = 500;
            var future = now + 2000;
            await database.StringSetAsync(key, future.ToString());

            var result = await cache.TryClaimNextCrawl(key, now, delay);

            result.IsSuccess.Should().BeTrue();
            result.Value.Allowed.Should().BeFalse();
            result.Value.NextAllowedTimestamp.Should().Be(future);

            var stored = await database.StringGetAsync(key);
            stored.ToString().Should().Be(future.ToString());
        }
        finally
        {
            await database.KeyDeleteAsync(key);
            multiplexer.Dispose();
        }
    }

    [Fact]
    public async Task TryClaimNextCrawl_WhenKeyInPast_AllowsAndUpdatesTimestamp()
    {
        if (!TryCreateCache(out var cache, out var database, out var multiplexer))
        {
            return;
        }
        var key = $"test:next:{Guid.NewGuid():N}";
        try
        {
            const long now = 1000;
            const long delay = 500;
            var past = now - 100;
            await database.StringSetAsync(key, past.ToString());

            var result = await cache.TryClaimNextCrawl(key, now, delay);

            result.IsSuccess.Should().BeTrue();
            result.Value.Allowed.Should().BeTrue();
            result.Value.NextAllowedTimestamp.Should().Be(now + delay);

            var stored = await database.StringGetAsync(key);
            stored.ToString().Should().Be((now + delay).ToString());
        }
        finally
        {
            await database.KeyDeleteAsync(key);
            multiplexer.Dispose();
        }
    }

    [Fact]
    public async Task Dequeue_WhenDue_ReturnsOnlyDueItems()
    {
        if (!TryCreateCache(out var cache, out var database, out var multiplexer))
        {
            return;
        }
        var queueKey = CacheKey.DueQueue();
        try
        {
            await database.KeyDeleteAsync(queueKey);

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var duePayload = $"payload:{Guid.NewGuid():N}";
            var futurePayload = $"payload:{Guid.NewGuid():N}";

            await cache.EnqueueUrlForCrawl(duePayload, now - 1);
            await cache.EnqueueUrlForCrawl(futurePayload, now + 10_000);

            var result = await cache.Dequeue(now, 10);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle(p => p == duePayload);

            var futureScore = await database.SortedSetScoreAsync(queueKey, futurePayload);
            futureScore.Should().Be(now + 10_000);
        }
        finally
        {
            await database.KeyDeleteAsync(queueKey);
            multiplexer.Dispose();
        }
    }

    [Fact]
    public async Task Dequeue_WhenNoDue_ReturnsEmpty()
    {
        if (!TryCreateCache(out var cache, out var database, out var multiplexer))
        {
            return;
        }
        var queueKey = CacheKey.DueQueue();
        try
        {
            await database.KeyDeleteAsync(queueKey);

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var futurePayload = $"payload:{Guid.NewGuid():N}";
            await cache.EnqueueUrlForCrawl(futurePayload, now + 10_000);

            var result = await cache.Dequeue(now, 10);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
        finally
        {
            await database.KeyDeleteAsync(queueKey);
            multiplexer.Dispose();
        }
    }

    private static bool TryCreateCache(
        out RedisCache cache,
        out IDatabase database,
        out IConnectionMultiplexer multiplexer)
    {
        var connectionString = Environment.GetEnvironmentVariable("SAMOBOT_TEST_REDIS") ?? "localhost:6379";

        try
        {
            var configuration = ConfigurationOptions.Parse(connectionString);
            multiplexer = ConnectionMultiplexer.Connect(configuration);

            if (!multiplexer.IsConnected)
            {
                multiplexer.Dispose();
                cache = null!;
                database = null!;
                multiplexer = null!;
                return false;
            }

            cache = new RedisCache(
                multiplexer,
                Options.Create(new RedisOptions
                {
                    ConnectionString = connectionString,
                    Database = TestDatabase
                }),
                NullLogger<RedisCache>.Instance);

            database = multiplexer.GetDatabase(TestDatabase);

            return true;
        }
        catch (RedisConnectionException)
        {
            cache = null!;
            database = null!;
            multiplexer = null!;
            return false;
        }
        catch (RedisTimeoutException)
        {
            cache = null!;
            database = null!;
            multiplexer = null!;
            return false;
        }
    }
}
