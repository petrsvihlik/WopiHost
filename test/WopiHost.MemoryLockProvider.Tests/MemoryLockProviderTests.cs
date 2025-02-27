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
    public void AddLock_ShouldAddLock()
    {
        // Arrange
        var fileId = "file1";
        var lockId = "lock1";

        // Act
        var lockInfo = _lockProvider.AddLock(fileId, lockId);

        // Assert
        Assert.NotNull(lockInfo);
        Assert.Equal(fileId, lockInfo.FileId);
        Assert.Equal(lockId, lockInfo.LockId);
    }

    [Fact]
    public void TryGetLock_ShouldReturnTrue_WhenLockExists()
    {
        // Arrange
        var fileId = "file2";
        var lockId = "lock2";
        _lockProvider.AddLock(fileId, lockId);

        // Act
        var result = _lockProvider.TryGetLock(fileId, out var lockInfo);

        // Assert
        Assert.True(result);
        Assert.NotNull(lockInfo);
        Assert.Equal(fileId, lockInfo.FileId);
        Assert.Equal(lockId, lockInfo.LockId);
    }

    [Fact]
    public void TryGetLock_ShouldReturnFalse_WhenLockDoesNotExist()
    {
        // Act
        var result = _lockProvider.TryGetLock("nonexistentFile", out var lockInfo);

        // Assert
        Assert.False(result);
        Assert.Null(lockInfo);
    }

    [Fact]
    public void RefreshLock_ShouldUpdateLock()
    {
        // Arrange
        var fileId = "file3";
        var lockId = "lock3";
        _lockProvider.AddLock(fileId, lockId);

        // Act
        var result = _lockProvider.RefreshLock(fileId);

        // Assert
        Assert.True(result);
        _lockProvider.TryGetLock(fileId, out var lockInfo);
        Assert.NotNull(lockInfo);
        Assert.Equal(fileId, lockInfo.FileId);
        Assert.Equal(lockId, lockInfo.LockId);
    }

    [Fact]
    public void RemoveLock_ShouldRemoveLock()
    {
        // Arrange
        var fileId = "file4";
        var lockId = "lock4";
        _lockProvider.AddLock(fileId, lockId);

        // Act
        var result = _lockProvider.RemoveLock(fileId);

        // Assert
        Assert.True(result);
        var lockExists = _lockProvider.TryGetLock(fileId, out var lockInfo);
        Assert.False(lockExists);
        Assert.Null(lockInfo);
    }
}
