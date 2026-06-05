using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.MemoryLockProvider.Tests;

/// <summary>
/// Provider-specific tests for <see cref="MemoryLockProvider"/> that can't run through the
/// shared <see cref="WopiHost.Abstractions.Testing.LockProviderConformanceTests"/> harness —
/// today that's just the direct stale-state seeding, which depends on the impl-specific backing
/// dictionary. Cross-impl behavior (add/get/refresh/remove/expiry/CAS/comparer) is covered by
/// <see cref="MemoryLockProviderConformanceTests"/>.
/// </summary>
public class MemoryLockProviderTests
{
    [Fact]
    public async Task GetLockAsync_ExpiredLock_RemovesAndReturnsNull_ViaDirectStateSeed()
    {
        // Seeds a stale record directly into the provider's per-instance state. The
        // TimeProvider-based expiry path is covered by the conformance suite; this case
        // additionally exercises the "entry whose DateCreated predates the clock" eviction
        // branch with a system clock (no fake), which the conformance suite can't model
        // without reaching into impl-specific state.
        var provider = new MemoryLockProvider(NullLogger<MemoryLockProvider>.Instance);
        var fileId = $"expired-direct-{Guid.NewGuid()}";
        provider.SeedLockForTesting(fileId, new WopiLockInfo
        {
            FileId = fileId,
            LockId = "stale",
            DateCreated = DateTimeOffset.UtcNow.AddHours(-1),
        });

        var found = await provider.GetLockAsync(fileId);

        Assert.Null(found);
        Assert.False(provider.ContainsLockForTesting(fileId));
    }

    [Fact]
    public void TwoInstances_OwnIndependentLockState()
    {
        // State must be per-instance: a static dictionary would make two instances share a single
        // lock store. A lock seeded into one provider must not appear on a sibling.
        var providerA = new MemoryLockProvider(NullLogger<MemoryLockProvider>.Instance);
        var providerB = new MemoryLockProvider(NullLogger<MemoryLockProvider>.Instance);

        var fileId = $"isolation-{Guid.NewGuid()}";
        providerA.SeedLockForTesting(fileId, new WopiLockInfo
        {
            FileId = fileId,
            LockId = "only-on-A",
            DateCreated = DateTimeOffset.UtcNow,
        });

        Assert.True(providerA.ContainsLockForTesting(fileId));
        Assert.False(providerB.ContainsLockForTesting(fileId));
    }
}
