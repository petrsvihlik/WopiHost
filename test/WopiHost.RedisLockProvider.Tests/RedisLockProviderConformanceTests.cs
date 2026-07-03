using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using WopiHost.Abstractions.Testing;
using Xunit;

namespace WopiHost.RedisLockProvider.Tests;

/// <summary>
/// Runs the shared <see cref="LockProviderConformanceTests"/> against <see cref="WopiRedisLockProvider"/>
/// using a real Redis 7 container (via Testcontainers.Redis) shared across the collection.
/// </summary>
/// <remarks>
/// Each test gets a fresh provider built around a GUID-suffixed key prefix so case-level state is
/// isolated even though the Redis instance is shared. The single shared connection-multiplexer
/// avoids per-test TCP/handshake churn.
/// </remarks>
[Collection(RedisCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RedisLockProviderConformanceTests(RedisFixture redis) : LockProviderConformanceTests
{
    /// <inheritdoc />
    protected override ILockProviderTestFactory Factory { get; } = new RedisFactory(redis);

    private sealed class RedisFactory(RedisFixture redis) : ILockProviderTestFactory
    {
        public async Task<IWopiLockProvider> CreateAsync(TimeProvider timeProvider, IWopiLockComparer? lockComparer = null)
        {
            var multiplexer = await redis.CreateMultiplexerAsync();
            var prefix = $"wopi:lock:test:{Guid.NewGuid():N}:";
            return new WopiRedisLockProvider(
                multiplexer,
                NullLogger<WopiRedisLockProvider>.Instance,
                prefix,
                timeProvider,
                lockComparer);
        }
    }
}
