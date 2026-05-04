using System.Reflection;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace WopiHost.AzureLockProvider.Tests;

/// <summary>
/// Targeted tests for error and edge paths in <see cref="WopiAzureLockProvider"/> that the happy-path
/// tests don't reach: malformed metadata, expired locks, broken leases, etc.
/// </summary>
[Collection(AzuriteCollection.Name)]
public class WopiAzureLockProviderEdgeCaseTests(AzuriteFixture azurite)
{
    private async Task<(WopiAzureLockProvider provider, BlobContainerClient container)> CreateProviderAsync()
    {
        var serviceClient = azurite.CreateBlobServiceClient();
        var container = serviceClient.GetBlobContainerClient($"wopi-locks-edge-{Guid.NewGuid():N}");
        await container.CreateIfNotExistsAsync();
        var provider = new WopiAzureLockProvider(container, NullLogger<WopiAzureLockProvider>.Instance);
        return (provider, container);
    }

    private static BlobClient GetLockBlob(WopiAzureLockProvider provider, string fileId)
    {
        var method = typeof(WopiAzureLockProvider)
            .GetMethod("GetLockBlob", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GetLockBlob not found");
        return (BlobClient)method.Invoke(provider, [fileId])!;
    }

    [Fact]
    public async Task RefreshLockAsync_ExpiredLock_ReturnsFalse()
    {
        var (provider, _) = await CreateProviderAsync();
        var lockBlob = GetLockBlob(provider, "file-expired-refresh");
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

        var refreshed = await provider.RefreshLockAsync("file-expired-refresh");

        Assert.False(refreshed);
    }

    [Fact]
    public async Task RefreshLockAsync_MissingLeaseIdInMetadata_ReturnsFalse()
    {
        var (provider, _) = await CreateProviderAsync();
        var lockBlob = GetLockBlob(provider, "file-no-leaseid");
        using (var empty = new MemoryStream([]))
        {
            await lockBlob.UploadAsync(empty);
        }
        await lockBlob.SetMetadataAsync(new Dictionary<string, string>
        {
            [WopiAzureLockProvider.LockIdKey] = "lock-A",
            [WopiAzureLockProvider.CreatedKey] = DateTimeOffset.UtcNow.ToString("O"),
            // No LeaseIdKey
        });

        var refreshed = await provider.RefreshLockAsync("file-no-leaseid");

        Assert.False(refreshed);
    }

    [Fact]
    public async Task RefreshLockAsync_LeaseGoneOrMismatched_ReturnsFalse()
    {
        // RefreshLockAsync recovers the leaseId from metadata. If the actual lease has been broken
        // since AddLock (crash, manual intervention, lease GC), RenewAsync throws 412/409 and refresh returns false.
        var (provider, _) = await CreateProviderAsync();
        var info = await provider.AddLockAsync("file-broken-lease", "lock-A");
        Assert.NotNull(info);

        // Externally break the lease so the renew fails.
        var lockBlob = GetLockBlob(provider, "file-broken-lease");
        await lockBlob.GetBlobLeaseClient().BreakAsync(breakPeriod: TimeSpan.Zero);

        var refreshed = await provider.RefreshLockAsync("file-broken-lease");

        Assert.False(refreshed);
    }

    [Fact]
    public async Task RemoveLockAsync_NoLeaseInMetadata_DeletesBlob()
    {
        var (provider, _) = await CreateProviderAsync();
        var lockBlob = GetLockBlob(provider, "file-no-lease-remove");
        using (var empty = new MemoryStream([]))
        {
            await lockBlob.UploadAsync(empty);
        }
        await lockBlob.SetMetadataAsync(new Dictionary<string, string>
        {
            [WopiAzureLockProvider.LockIdKey] = "lock-A",
            [WopiAzureLockProvider.CreatedKey] = DateTimeOffset.UtcNow.ToString("O"),
            // No LeaseIdKey — the metadata-only branch in RemoveLock.
        });

        var removed = await provider.RemoveLockAsync("file-no-lease-remove");

        Assert.True(removed);
        Assert.False(await lockBlob.ExistsAsync());
    }

    [Fact]
    public async Task RemoveLockAsync_StaleLeaseIdInMetadata_FallsBackToBreak()
    {
        // The metadata's lease GUID doesn't match any actual active lease — Release will fail with
        // a 409/412 LeaseIdMismatch. The fallback path break-leases and proceeds to delete.
        var (provider, _) = await CreateProviderAsync();
        var lockBlob = GetLockBlob(provider, "file-stale-lease-remove");
        using (var empty = new MemoryStream([]))
        {
            await lockBlob.UploadAsync(empty);
        }
        await lockBlob.SetMetadataAsync(new Dictionary<string, string>
        {
            [WopiAzureLockProvider.LockIdKey] = "lock-A",
            [WopiAzureLockProvider.LeaseIdKey] = Guid.NewGuid().ToString(), // never acquired
            [WopiAzureLockProvider.CreatedKey] = DateTimeOffset.UtcNow.ToString("O"),
        });

        var removed = await provider.RemoveLockAsync("file-stale-lease-remove");

        Assert.True(removed);
        Assert.False(await lockBlob.ExistsAsync());
    }

    [Fact]
    public async Task GetLockAsync_MalformedCreatedTimestamp_ReturnsNull()
    {
        // Hits the TryReadLock parse-failure branch (returns false → outer GetLockAsync returns null).
        var (provider, _) = await CreateProviderAsync();
        var lockBlob = GetLockBlob(provider, "file-malformed-created");
        using (var empty = new MemoryStream([]))
        {
            await lockBlob.UploadAsync(empty);
        }
        await lockBlob.SetMetadataAsync(new Dictionary<string, string>
        {
            [WopiAzureLockProvider.LockIdKey] = "lock-A",
            [WopiAzureLockProvider.LeaseIdKey] = Guid.NewGuid().ToString(),
            [WopiAzureLockProvider.CreatedKey] = "not-a-real-iso-timestamp",
        });

        var info = await provider.GetLockAsync("file-malformed-created");

        Assert.Null(info);
    }

    [Fact]
    public async Task GetLockAsync_EmptyLockIdInMetadata_ReturnsNull()
    {
        // TryReadLock requires non-empty lock id; otherwise returns false.
        var (provider, _) = await CreateProviderAsync();
        var lockBlob = GetLockBlob(provider, "file-empty-lockid");
        using (var empty = new MemoryStream([]))
        {
            await lockBlob.UploadAsync(empty);
        }
        await lockBlob.SetMetadataAsync(new Dictionary<string, string>
        {
            [WopiAzureLockProvider.LockIdKey] = "",
            [WopiAzureLockProvider.LeaseIdKey] = Guid.NewGuid().ToString(),
            [WopiAzureLockProvider.CreatedKey] = DateTimeOffset.UtcNow.ToString("O"),
        });

        var info = await provider.GetLockAsync("file-empty-lockid");

        Assert.Null(info);
    }

    [Fact]
    public async Task AddLockAsync_BlobExistsButNoMetadata_TakesOver()
    {
        // Hits the "existing blob with no readable lock metadata" → break-lease + delete path,
        // then proceeds to upload + acquire.
        var (provider, _) = await CreateProviderAsync();
        var lockBlob = GetLockBlob(provider, "file-stale-blob");
        using (var empty = new MemoryStream([]))
        {
            await lockBlob.UploadAsync(empty);
        }
        // No metadata at all on the blob — bare zero-byte placeholder.

        var info = await provider.AddLockAsync("file-stale-blob", "fresh");

        Assert.NotNull(info);
        Assert.Equal("fresh", info.LockId);
    }

    [Fact]
    public async Task AddLockAsync_ConcurrentRace_OneWinsOthersReturnNull()
    {
        // Hits the 409-on-Upload race-lost branch: both callers pass TryGetProperties (blob doesn't
        // exist yet), then race on UploadAsync(overwrite:false). Azurite serializes the writes — the
        // loser sees 409 and goes through the LogLockAddRaceLost catch.
        var (provider, _) = await CreateProviderAsync();
        var fileId = $"file-race-{Guid.NewGuid():N}";

        var tasks = Enumerable.Range(0, 6)
            .Select(i => provider.AddLockAsync(fileId, $"lock-{i}"))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.Single(results, r => r is not null);
        Assert.Equal(results.Length - 1, results.Count(r => r is null));
    }

    [Fact]
    public async Task Constructor_NullArgs_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WopiAzureLockProvider(null!, NullLogger<WopiAzureLockProvider>.Instance));

        var serviceClient = azurite.CreateBlobServiceClient();
        var container = serviceClient.GetBlobContainerClient("ctor-test");
        Assert.Throws<ArgumentNullException>(
            () => new WopiAzureLockProvider(container, null!));
    }
}
