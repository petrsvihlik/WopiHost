using System.Reflection;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using Xunit;

namespace WopiHost.AzureLockProvider.Tests;

[Collection(AzuriteCollection.Name)]
public class WopiAzureLockProviderTests(AzuriteFixture azurite)
{
    private async Task<(WopiAzureLockProvider provider, BlobContainerClient container)> CreateProviderAsync()
    {
        var serviceClient = azurite.CreateBlobServiceClient();
        var container = serviceClient.GetBlobContainerClient($"wopi-locks-test-{Guid.NewGuid():N}");
        await container.CreateIfNotExistsAsync();
        var provider = new WopiAzureLockProvider(container, NullLogger<WopiAzureLockProvider>.Instance);
        return (provider, container);
    }

    [Fact]
    public async Task AddLockAsync_NoExistingLock_Succeeds()
    {
        var (provider, _) = await CreateProviderAsync();

        var info = await provider.AddLockAsync("file-1", "lock-A");

        Assert.NotNull(info);
        Assert.Equal("file-1", info.FileId);
        Assert.Equal("lock-A", info.LockId);
        Assert.False(info.Expired);
    }

    [Fact]
    public async Task AddLockAsync_WhenAlreadyLocked_ReturnsNull()
    {
        var (provider, _) = await CreateProviderAsync();
        await provider.AddLockAsync("file-2", "lock-A");

        var second = await provider.AddLockAsync("file-2", "lock-B");

        Assert.Null(second);
    }

    [Fact]
    public async Task GetLockAsync_ReturnsLock_WhenPresent()
    {
        var (provider, _) = await CreateProviderAsync();
        await provider.AddLockAsync("file-3", "lock-A");

        var info = await provider.GetLockAsync("file-3");

        Assert.NotNull(info);
        Assert.Equal("lock-A", info.LockId);
    }

    [Fact]
    public async Task GetLockAsync_ReturnsNull_WhenAbsent()
    {
        var (provider, _) = await CreateProviderAsync();

        var info = await provider.GetLockAsync("file-never-locked");

        Assert.Null(info);
    }

    [Fact]
    public async Task RefreshLockAsync_UpdatesTimestamp_AndOptionallyChangesLockId()
    {
        var (provider, _) = await CreateProviderAsync();
        var original = await provider.AddLockAsync("file-4", "lock-A");
        Assert.NotNull(original);

        await Task.Delay(50);
        var refreshed = await provider.RefreshLockAsync("file-4", lockId: "lock-B");

        Assert.True(refreshed);
        var info = await provider.GetLockAsync("file-4");
        Assert.NotNull(info);
        Assert.Equal("lock-B", info.LockId);
        Assert.True(info.DateCreated > original.DateCreated);
    }

    [Fact]
    public async Task RefreshLockAsync_NoExistingLock_ReturnsFalse()
    {
        var (provider, _) = await CreateProviderAsync();

        var result = await provider.RefreshLockAsync("file-no-lock");

        Assert.False(result);
    }

    [Fact]
    public async Task RemoveLockAsync_ClearsLock()
    {
        var (provider, _) = await CreateProviderAsync();
        await provider.AddLockAsync("file-5", "lock-A");

        var removed = await provider.RemoveLockAsync("file-5");

        Assert.True(removed);
        Assert.Null(await provider.GetLockAsync("file-5"));
    }

    [Fact]
    public async Task RemoveLockAsync_NoLock_ReturnsFalse()
    {
        var (provider, _) = await CreateProviderAsync();

        var result = await provider.RemoveLockAsync("file-no-lock");

        Assert.False(result);
    }

    [Fact]
    public async Task AddLockAsync_AfterPreviousLockRemoved_Succeeds()
    {
        var (provider, _) = await CreateProviderAsync();
        await provider.AddLockAsync("file-6", "lock-A");
        await provider.RemoveLockAsync("file-6");

        var second = await provider.AddLockAsync("file-6", "lock-B");

        Assert.NotNull(second);
        Assert.Equal("lock-B", second.LockId);
    }

    [Fact]
    public async Task GetLockAsync_ExpiredLock_ReturnsNull_AndEvicts()
    {
        // Seed an expired entry directly so we don't have to wait 30 minutes.
        var (provider, _) = await CreateProviderAsync();
        var lockBlob = GetLockBlob(provider, "file-stale");

        // Stage: create the blob, set metadata with an old timestamp.
        using (var empty = new MemoryStream([]))
        {
            await lockBlob.UploadAsync(empty);
        }
        await lockBlob.SetMetadataAsync(new Dictionary<string, string>
        {
            [WopiAzureLockProvider.LockIdKey] = "ancient",
            [WopiAzureLockProvider.LeaseIdKey] = Guid.NewGuid().ToString(),
            [WopiAzureLockProvider.CreatedKey] = DateTimeOffset.UtcNow.AddHours(-2).ToString("O"),
        });

        var info = await provider.GetLockAsync("file-stale");

        Assert.Null(info);
        Assert.False(await lockBlob.ExistsAsync());
    }

    [Fact]
    public async Task AddLockAsync_TakesOverExpiredLock()
    {
        var (provider, _) = await CreateProviderAsync();
        var lockBlob = GetLockBlob(provider, "file-takeover");

        using (var empty = new MemoryStream([]))
        {
            await lockBlob.UploadAsync(empty);
        }
        // Acquire an infinite lease so the stale lock has a real Azure lease (mirroring a real "crashed holder" scenario).
        var leaseId = Guid.NewGuid().ToString();
        await lockBlob.GetBlobLeaseClient(leaseId).AcquireAsync(TimeSpan.FromSeconds(-1));
        await lockBlob.SetMetadataAsync(
            new Dictionary<string, string>
            {
                [WopiAzureLockProvider.LockIdKey] = "ancient",
                [WopiAzureLockProvider.LeaseIdKey] = leaseId,
                [WopiAzureLockProvider.CreatedKey] = DateTimeOffset.UtcNow.AddHours(-2).ToString("O"),
            },
            conditions: new BlobRequestConditions { LeaseId = leaseId });

        var info = await provider.AddLockAsync("file-takeover", "fresh-lock");

        Assert.NotNull(info);
        Assert.Equal("fresh-lock", info.LockId);
    }

    private static BlobClient GetLockBlob(WopiAzureLockProvider provider, string fileId)
    {
        // Reach into the private GetLockBlob to address the same blob in setup helpers.
        var method = typeof(WopiAzureLockProvider)
            .GetMethod("GetLockBlob", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GetLockBlob not found");
        return (BlobClient)method.Invoke(provider, [fileId])!;
    }
}
