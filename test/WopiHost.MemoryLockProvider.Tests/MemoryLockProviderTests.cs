using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.MemoryLockProvider.Tests;

public class MemoryLockProviderTests
{
    private readonly MemoryLockProvider _lockProvider;

    public MemoryLockProviderTests()
    {
        _lockProvider = new MemoryLockProvider(NullLogger<MemoryLockProvider>.Instance);
    }

    [Fact]
    public async Task AddLock_ShouldAddLock()
    {
        var fileId = $"add-{Guid.NewGuid()}";
        var lockId = "lock1";

        var lockInfo = await _lockProvider.AddLockAsync(fileId, lockId);

        Assert.NotNull(lockInfo);
        Assert.Equal(fileId, lockInfo.FileId);
        Assert.Equal(lockId, lockInfo.LockId);
    }

    [Fact]
    public async Task GetLockAsync_ShouldReturnLock_WhenLockExists()
    {
        var fileId = $"get-{Guid.NewGuid()}";
        var lockId = "lock2";
        await _lockProvider.AddLockAsync(fileId, lockId);

        var lockInfo = await _lockProvider.GetLockAsync(fileId);

        Assert.NotNull(lockInfo);
        Assert.Equal(fileId, lockInfo.FileId);
        Assert.Equal(lockId, lockInfo.LockId);
    }

    [Fact]
    public async Task GetLockAsync_ShouldReturnNull_WhenLockDoesNotExist()
    {
        var lockInfo = await _lockProvider.GetLockAsync($"missing-{Guid.NewGuid()}");

        Assert.Null(lockInfo);
    }

    [Fact]
    public async Task RefreshLock_ShouldUpdateLock()
    {
        var fileId = $"refresh-{Guid.NewGuid()}";
        var lockId = "lock3";
        await _lockProvider.AddLockAsync(fileId, lockId);

        var result = await _lockProvider.RefreshLockAsync(fileId);

        Assert.True(result);
        var lockInfo = await _lockProvider.GetLockAsync(fileId);
        Assert.NotNull(lockInfo);
        Assert.Equal(fileId, lockInfo.FileId);
        Assert.Equal(lockId, lockInfo.LockId);
    }

    [Fact]
    public async Task RemoveLock_ShouldRemoveLock()
    {
        var fileId = $"remove-{Guid.NewGuid()}";
        var lockId = "lock4";
        await _lockProvider.AddLockAsync(fileId, lockId);

        var result = await _lockProvider.RemoveLockAsync(fileId);

        Assert.True(result);
        var lockInfo = await _lockProvider.GetLockAsync(fileId);
        Assert.Null(lockInfo);
    }

    [Fact]
    public async Task AddLock_DuplicateFileId_ReturnsNull()
    {
        var fileId = $"dup-{Guid.NewGuid()}";
        await _lockProvider.AddLockAsync(fileId, "first");

        var second = await _lockProvider.AddLockAsync(fileId, "second");

        Assert.Null(second);
    }

    [Fact]
    public async Task RefreshLock_NoExistingLock_ReturnsFalse()
    {
        var fileId = $"missing-{Guid.NewGuid()}";

        var result = await _lockProvider.RefreshLockAsync(fileId);

        Assert.False(result);
    }

    [Fact]
    public async Task RemoveLock_NoExistingLock_ReturnsFalse()
    {
        var fileId = $"remove-missing-{Guid.NewGuid()}";

        var result = await _lockProvider.RemoveLockAsync(fileId);

        Assert.False(result);
    }

    [Fact]
    public async Task GetLockAsync_ExpiredLock_RemovesAndReturnsNull()
    {
        // The provider's AddLockAsync always stamps DateCreated with UtcNow, so the
        // only way to seed an expired entry is to inject one directly into the
        // private static dictionary backing the provider.
        var fileId = $"expired-{Guid.NewGuid()}";
        var locks = GetSharedLockDictionary();
        locks[fileId] = new WopiLockInfo
        {
            FileId = fileId,
            LockId = "stale",
            DateCreated = DateTimeOffset.UtcNow.AddHours(-1),
        };

        var found = await _lockProvider.GetLockAsync(fileId);

        Assert.Null(found);
        Assert.False(locks.ContainsKey(fileId));
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_MatchingExpectedLock_SwapsAtomically()
    {
        var fileId = $"swap-{Guid.NewGuid()}";
        await _lockProvider.AddLockAsync(fileId, "old-lock");

        var swapped = await _lockProvider.TryUnlockAndRelockAsync(fileId, "new-lock", expectedExistingLockId: "old-lock");

        Assert.True(swapped);
        var info = await _lockProvider.GetLockAsync(fileId);
        Assert.NotNull(info);
        Assert.Equal("new-lock", info.LockId);
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_MismatchedExpectedLock_ReturnsFalseAndDoesNotMutate()
    {
        var fileId = $"swap-mismatch-{Guid.NewGuid()}";
        await _lockProvider.AddLockAsync(fileId, "current-lock");

        var swapped = await _lockProvider.TryUnlockAndRelockAsync(fileId, "new-lock", expectedExistingLockId: "stale-cached-lock");

        Assert.False(swapped);
        var info = await _lockProvider.GetLockAsync(fileId);
        Assert.NotNull(info);
        Assert.Equal("current-lock", info.LockId);
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_NoExistingLock_ReturnsFalse()
    {
        var fileId = $"swap-missing-{Guid.NewGuid()}";

        var swapped = await _lockProvider.TryUnlockAndRelockAsync(fileId, "new-lock", expectedExistingLockId: "anything");

        Assert.False(swapped);
        Assert.Null(await _lockProvider.GetLockAsync(fileId));
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_ConcurrentSwapBetweenObservationAndCAS_DoesNotStealLock()
    {
        // The race we're protecting against: caller A reads the lock as "old-lock", validates,
        // and is about to relock to "A-new". Before A's CAS lands, caller B successfully relocks
        // to "B-new". Without atomic CAS, A's swap would silently overwrite B's lock.
        var fileId = $"race-{Guid.NewGuid()}";
        await _lockProvider.AddLockAsync(fileId, "old-lock");

        // Simulate caller B winning the race first.
        var bSwapped = await _lockProvider.TryUnlockAndRelockAsync(fileId, "B-new", expectedExistingLockId: "old-lock");
        Assert.True(bSwapped);

        // Caller A still thinks the lock is "old-lock" — its CAS must fail.
        var aSwapped = await _lockProvider.TryUnlockAndRelockAsync(fileId, "A-new", expectedExistingLockId: "old-lock");
        Assert.False(aSwapped);

        var info = await _lockProvider.GetLockAsync(fileId);
        Assert.NotNull(info);
        Assert.Equal("B-new", info.LockId);
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_JsonShapedLockWithExtraProperty_FailsUnderDefaultStrictCompare()
    {
        // Default IWopiLockComparer is OrdinalWopiLockComparer (byte-exact). Pin the strict
        // behavior under the default — if M365 for the Web ever flakes on lock mismatches in
        // production, the fix is to swap in JsonShapedWopiLockComparer (or a custom comparer
        // tailored to the observed mutation), not to relax this test.
        var fileId = $"json-lock-{Guid.NewGuid()}";
        var clientLock = """{"S":"abc-123","F":4}""";
        await _lockProvider.AddLockAsync(fileId, clientLock);

        var clientLockMutated = """{"S":"abc-123","F":4,"V":1}""";

        var swapped = await _lockProvider.TryUnlockAndRelockAsync(
            fileId,
            newLockId: "next-lock",
            expectedExistingLockId: clientLockMutated);

        Assert.False(swapped);
        var info = await _lockProvider.GetLockAsync(fileId);
        Assert.NotNull(info);
        Assert.Equal(clientLock, info.LockId);
    }

    [Fact]
    public async Task TryUnlockAndRelockAsync_JsonShapedLockWithExtraProperty_SwapsUnderJsonShapedComparer()
    {
        // Same scenario, but with the tolerant comparer plugged in: the swap should succeed
        // because the OOS-style lock-id mutation only added a property; the underlying S-field
        // identity hasn't changed.
        var provider = new MemoryLockProvider(NullLogger<MemoryLockProvider>.Instance, new JsonShapedWopiLockComparer());
        var fileId = $"json-lock-tolerant-{Guid.NewGuid()}";
        var clientLock = """{"S":"abc-123","F":4}""";
        await provider.AddLockAsync(fileId, clientLock);

        var clientLockMutated = """{"S":"abc-123","F":4,"V":1}""";

        var swapped = await provider.TryUnlockAndRelockAsync(
            fileId,
            newLockId: "next-lock",
            expectedExistingLockId: clientLockMutated);

        Assert.True(swapped);
        var info = await provider.GetLockAsync(fileId);
        Assert.NotNull(info);
        Assert.Equal("next-lock", info.LockId);
    }

    private static ConcurrentDictionary<string, WopiLockInfo> GetSharedLockDictionary()
    {
        var field = typeof(MemoryLockProvider)
            .GetField("locks", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MemoryLockProvider.locks field not found");
        return (ConcurrentDictionary<string, WopiLockInfo>)field.GetValue(null)!;
    }
}
