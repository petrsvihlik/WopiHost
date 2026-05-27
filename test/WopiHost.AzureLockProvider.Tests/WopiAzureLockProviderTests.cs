using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace WopiHost.AzureLockProvider.Tests;

/// <summary>
/// Provider-specific tests for <see cref="WopiAzureLockProvider"/> that can't run through the
/// shared <see cref="WopiHost.Abstractions.Testing.LockProviderConformanceTests"/> harness —
/// scenarios that depend on Azure-blob-specific shape (direct metadata seeding to simulate a
/// stale record, blob-lease takeover with a real Azure lease, etc.). Cross-impl behavior
/// (add/get/refresh/remove/expiry/CAS/comparer) is covered by
/// <see cref="AzureLockProviderConformanceTests"/>.
/// </summary>
[Trait("Category", "Integration")]
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
    public async Task GetLockAsync_ExpiredLock_ReturnsNull_AndEvicts_ViaDirectMetadataSeed()
    {
        // Conformance suite covers expiry via TimeProvider injection; this case additionally
        // exercises the wall-clock path against a stale on-blob CreatedKey written outside the
        // provider (mirroring what an instance that crashed mid-write would leave behind).
        var (provider, _) = await CreateProviderAsync();
        var lockBlob = GetLockBlob(provider, "file-stale");

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
        // Specifically exercises the Azure blob-lease branch — the previous lock holder
        // acquired an infinite Azure lease before crashing. New acquisition has to BREAK that
        // lease, not merely overwrite metadata. The conformance suite can't model an Azure-lease
        // state from a pure-interface position.
        var (provider, _) = await CreateProviderAsync();
        var lockBlob = GetLockBlob(provider, "file-takeover");

        using (var empty = new MemoryStream([]))
        {
            await lockBlob.UploadAsync(empty);
        }
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
        // GetLockBlob is internal — visible to this assembly via the auto-wired
        // InternalsVisibleTo in Directory.Build.props (WopiHost.AzureLockProvider →
        // WopiHost.AzureLockProvider.Tests).
        => provider.GetLockBlob(fileId);
}
