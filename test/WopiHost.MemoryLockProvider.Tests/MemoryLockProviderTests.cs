using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.MemoryLockProvider.Tests;

/// <summary>
/// Provider-specific tests for <see cref="MemoryLockProvider"/> that can't run through the
/// shared <see cref="WopiHost.Abstractions.Testing.LockProviderConformanceTests"/> harness —
/// today that's just the reflective stale-state seeding, which depends on the impl-specific
/// backing dictionary. Cross-impl behavior (add/get/refresh/remove/expiry/CAS/comparer) is
/// covered by <see cref="MemoryLockProviderConformanceTests"/>.
/// </summary>
public class MemoryLockProviderTests
{
    [Fact]
    public async Task GetLockAsync_ExpiredLock_RemovesAndReturnsNull_ViaDirectStateSeed()
    {
        // Seeds a stale record by writing directly into the provider's per-instance backing
        // dictionary. The TimeProvider-based expiry path is covered by the conformance suite;
        // this case additionally exercises the "I observed an entry whose DateCreated predates
        // my clock" eviction branch with a system clock (no fake), which the conformance suite
        // can't model without reaching into private state.
        var provider = new MemoryLockProvider(NullLogger<MemoryLockProvider>.Instance);
        var fileId = $"expired-direct-{Guid.NewGuid()}";
        var locks = GetInstanceLockDictionary(provider);
        locks[fileId] = new WopiLockInfo
        {
            FileId = fileId,
            LockId = "stale",
            DateCreated = DateTimeOffset.UtcNow.AddHours(-1),
        };

        var found = await provider.GetLockAsync(fileId);

        Assert.Null(found);
        Assert.False(locks.ContainsKey(fileId));
    }

    [Fact]
    public void TwoInstances_OwnIndependentLockState()
    {
        // Pins #380 item 2.3 — pre-fix the provider used a static dictionary, so two instances
        // shared a single lock store. Now state is per-instance; a lock added through one
        // provider must not appear on a sibling.
        var providerA = new MemoryLockProvider(NullLogger<MemoryLockProvider>.Instance);
        var providerB = new MemoryLockProvider(NullLogger<MemoryLockProvider>.Instance);

        var locksA = GetInstanceLockDictionary(providerA);
        var locksB = GetInstanceLockDictionary(providerB);

        Assert.NotSame(locksA, locksB);
    }

    private static ConcurrentDictionary<string, WopiLockInfo> GetInstanceLockDictionary(MemoryLockProvider provider)
    {
        var field = typeof(MemoryLockProvider)
            .GetField("_locks", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MemoryLockProvider._locks field not found");
        return (ConcurrentDictionary<string, WopiLockInfo>)field.GetValue(provider)!;
    }
}
