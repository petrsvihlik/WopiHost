using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using WopiHost.Abstractions;
using WopiHost.Abstractions.Testing;
using Xunit;

namespace WopiHost.RedisLockProvider.Tests;

/// <summary>
/// Redis-specific coverage that the compare-and-swap paths (<see cref="WopiRedisLockProvider.RefreshLockAsync"/>
/// and <see cref="WopiRedisLockProvider.TryUnlockAndRelockAsync"/>) <em>proactively evict</em> an expired
/// record instead of leaving it for Redis's TTL to reap.
/// </summary>
/// <remarks>
/// The shared <see cref="LockProviderConformanceTests"/> already asserts these paths return <c>false</c>
/// past expiry and that a follow-up <c>GetLockAsync</c> returns <c>null</c> — but <c>GetLockAsync</c>
/// evicts on read too, so that black-box assertion can't tell whether the CAS path itself cleaned up.
/// These tests reach past the abstraction and check the raw Redis key directly, so they fail if the CAS
/// path regresses to relying on TTL / the next read. Under the fake clock the Redis server clock never
/// moves, so the server-side <c>EX</c> would not evict at all — the only thing that can delete the key
/// here is the provider's own eviction.
/// </remarks>
[Collection(RedisCollection.Name)]
public sealed class WopiRedisLockProviderEvictionTests(RedisFixture redis)
{
    private async Task<(WopiRedisLockProvider Sut, IDatabase Db, RedisKey Key)> CreateAsync(
        string fileId, ControllableTimeProvider clock)
    {
        var multiplexer = await redis.CreateMultiplexerAsync();
        var prefix = $"wopi:lock:evict-test:{Guid.NewGuid():N}:";
        var sut = new WopiRedisLockProvider(
            multiplexer, NullLogger<WopiRedisLockProvider>.Instance, prefix, clock);
        return (sut, multiplexer.GetDatabase(), prefix + fileId);
    }

    [Fact]
    public async Task RefreshLockAsync_PastExpiry_DeletesTheRedisKey()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        var fileId = $"refresh-evict-{Guid.NewGuid()}";
        var (sut, db, key) = await CreateAsync(fileId, clock);
        await sut.AddLockAsync(fileId, "lock-A");
        Assert.True(await db.KeyExistsAsync(key));

        clock.Now = clock.Now.AddMinutes(WopiLockInfo.ExpirationMinutes + 1);

        Assert.False(await sut.RefreshLockAsync(fileId, expectedExistingLockId: "lock-A"));
        // Key is gone because the CAS path evicted it — not because anything read it back.
        Assert.False(await db.KeyExistsAsync(key));
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_PastExpiry_DeletesTheRedisKey()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        var fileId = $"swap-evict-{Guid.NewGuid()}";
        var (sut, db, key) = await CreateAsync(fileId, clock);
        await sut.AddLockAsync(fileId, "old-lock");
        Assert.True(await db.KeyExistsAsync(key));

        clock.Now = clock.Now.AddMinutes(WopiLockInfo.ExpirationMinutes + 1);

        Assert.False(await sut.TryUnlockAndRelockAsync(fileId, "new-lock", expectedExistingLockId: "old-lock"));
        Assert.False(await db.KeyExistsAsync(key));
    }
}
