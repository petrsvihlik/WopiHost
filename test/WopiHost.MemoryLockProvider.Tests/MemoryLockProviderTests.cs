using System.Collections.Concurrent;
using System.Reflection;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.MemoryLockProvider.Tests;

public class MemoryLockProviderTests
{
    private readonly MemoryLockProvider _lockProvider;

    public MemoryLockProviderTests()
    {
        _lockProvider = new MemoryLockProvider();
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

    private static ConcurrentDictionary<string, WopiLockInfo> GetSharedLockDictionary()
    {
        var field = typeof(MemoryLockProvider)
            .GetField("locks", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MemoryLockProvider.locks field not found");
        return (ConcurrentDictionary<string, WopiLockInfo>)field.GetValue(null)!;
    }
}
