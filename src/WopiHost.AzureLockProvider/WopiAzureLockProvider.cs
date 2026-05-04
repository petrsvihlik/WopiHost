using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.AzureLockProvider;

/// <summary>
/// <see cref="IWopiLockProvider"/> backed by Azure Blob leases.
/// </summary>
/// <remarks>
/// <para>
/// For each <c>fileId</c>, the provider creates a placeholder blob in <see cref="WopiAzureLockProviderOptions.ContainerName"/>
/// and acquires an <em>infinite</em> blob lease. The Azure lease provides true cross-instance
/// exclusion (only one WopiHost instance can hold the lease at a time), while the blob's metadata
/// carries the WOPI-level state (the client-supplied lock id, the Azure lease GUID, and the
/// creation timestamp used to honour the WOPI 30-minute expiry).
/// </para>
/// <para>
/// Cross-instance coordination on refresh / remove works because the lease GUID is stored in blob
/// metadata: any instance that observes the lock can read it back and call renew/release. If the
/// instance that created the lock dies, the infinite lease survives until another instance
/// observes the WOPI-expiry has passed and explicitly breaks the lease in <see cref="GetLockAsync"/>.
/// </para>
/// </remarks>
/// <summary>Create the provider from a configured <see cref="BlobContainerClient"/>.</summary>
public partial class WopiAzureLockProvider(BlobContainerClient containerClient, ILogger<WopiAzureLockProvider> logger) : IWopiLockProvider
{
    internal const string LockIdKey = "wopi_lock_id";
    internal const string LeaseIdKey = "wopi_lease_id";
    internal const string CreatedKey = "wopi_created";

    private readonly BlobContainerClient containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
    private readonly ILogger<WopiAzureLockProvider> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SemaphoreSlim initLock = new(1, 1);
    private bool initialized;

    /// <inheritdoc />
    public async Task<WopiLockInfo?> GetLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);
        var blobClient = GetLockBlob(fileId);
        var props = await TryGetPropertiesAsync(blobClient, cancellationToken).ConfigureAwait(false);
        if (props is null || !TryReadLock(fileId, props, out var info))
        {
            return null;
        }
        if (!info.Expired)
        {
            return info;
        }

        // WOPI-expired. Break the lease so subsequent AddLock calls can take over, then evict.
        await TryBreakLeaseAsync(blobClient, cancellationToken).ConfigureAwait(false);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        LogLockExpired(logger, fileId, info.LockId);
        return null;
    }

    /// <inheritdoc />
    public async Task<WopiLockInfo?> AddLockAsync(string fileId, string lockId, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);
        var blobClient = GetLockBlob(fileId);

        // Honour WOPI expiry: a stale (>30 min old) lock can be taken over.
        var existing = await TryGetPropertiesAsync(blobClient, cancellationToken).ConfigureAwait(false);
        if (existing is not null && TryReadLock(fileId, existing, out var existingInfo) && !existingInfo.Expired)
        {
            LogLockAddRejected(logger, fileId, lockId, existingInfo.LockId);
            return null;
        }
        if (existing is not null)
        {
            // Either no valid metadata or expired. Break the lease and delete so we can start fresh.
            await TryBreakLeaseAsync(blobClient, cancellationToken).ConfigureAwait(false);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // Upload the placeholder. If two callers race here, exactly one wins (overwrite=false).
        try
        {
            using var empty = new MemoryStream([], writable: false);
            await blobClient.UploadAsync(empty, overwrite: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Raced with another caller that just created the blob.
            LogLockAddRaceLost(logger, fileId, lockId);
            return null;
        }

        var leaseId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var leaseClient = blobClient.GetBlobLeaseClient(leaseId);

        try
        {
            await leaseClient.AcquireAsync(TimeSpan.FromSeconds(-1), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex)
        {
            // Couldn't lease — clean up the placeholder so we don't leave a no-op blob behind.
            LogLeaseAcquireFailed(logger, ex, fileId);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return null;
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LockIdKey] = lockId,
            [LeaseIdKey] = leaseId,
            [CreatedKey] = now.ToString("O", CultureInfo.InvariantCulture),
        };
        await blobClient.SetMetadataAsync(metadata,
            conditions: new BlobRequestConditions { LeaseId = leaseId },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        LogLockAcquired(logger, fileId, lockId);
        return new WopiLockInfo { FileId = fileId, LockId = lockId, DateCreated = now };
    }

    /// <inheritdoc />
    public async Task<bool> RefreshLockAsync(string fileId, string? lockId = null, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);
        var blobClient = GetLockBlob(fileId);
        var props = await TryGetPropertiesAsync(blobClient, cancellationToken).ConfigureAwait(false);
        if (props is null || !TryReadLock(fileId, props, out var info))
        {
            return false;
        }
        if (info.Expired)
        {
            return false;
        }
        if (!props.Metadata.TryGetValue(LeaseIdKey, out var leaseId) || string.IsNullOrEmpty(leaseId))
        {
            return false;
        }

        // Renew the lease so the holder semantic stays alive (no-op for infinite leases but still
        // confirms the lease is still ours), then update timestamp + optional new lockId.
        var leaseClient = blobClient.GetBlobLeaseClient(leaseId);
        try
        {
            await leaseClient.RenewAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex)
        {
            LogLeaseRenewFailed(logger, ex, fileId);
            return false;
        }

        var updated = new Dictionary<string, string>(props.Metadata, StringComparer.Ordinal)
        {
            [LockIdKey] = lockId ?? info.LockId,
            [CreatedKey] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        };
        try
        {
            await blobClient.SetMetadataAsync(updated,
                conditions: new BlobRequestConditions { LeaseId = leaseId },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException ex)
        {
            LogLockMetadataUpdateFailed(logger, ex, fileId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);
        var blobClient = GetLockBlob(fileId);
        var props = await TryGetPropertiesAsync(blobClient, cancellationToken).ConfigureAwait(false);
        if (props is null)
        {
            return false;
        }
        if (props.Metadata.TryGetValue(LeaseIdKey, out var leaseId) && !string.IsNullOrEmpty(leaseId))
        {
            try
            {
                await blobClient.GetBlobLeaseClient(leaseId).ReleaseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
            {
                // Lease already gone or mismatch — fall back to break-lease so the delete can proceed.
                await TryBreakLeaseAsync(blobClient, cancellationToken).ConfigureAwait(false);
            }
        }
        var deleted = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (deleted.Value)
        {
            var removedLockId = props.Metadata.TryGetValue(LockIdKey, out var lid) ? lid : null;
            LogLockRemoved(logger, fileId, removedLockId);
        }
        return deleted.Value;
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (initialized)
        {
            return;
        }
        await initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (initialized)
            {
                return;
            }
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            initialized = true;
        }
        finally
        {
            initLock.Release();
        }
    }

    private BlobClient GetLockBlob(string fileId)
    {
        // Hash the fileId so any caller-supplied identifier is reduced to a safe blob name.
        var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fileId))).ToLowerInvariant();
        return containerClient.GetBlobClient(name);
    }

    private static async Task<BlobProperties?> TryGetPropertiesAsync(BlobClient blobClient, CancellationToken cancellationToken)
    {
        try
        {
            return await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static async Task TryBreakLeaseAsync(BlobClient blobClient, CancellationToken cancellationToken)
    {
        try
        {
            await blobClient.GetBlobLeaseClient().BreakAsync(breakPeriod: TimeSpan.Zero, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException)
        {
            // Best-effort — if the lease is already gone or the blob disappeared, we don't care.
        }
    }

    private static bool TryReadLock(string fileId, BlobProperties props, out WopiLockInfo info)
    {
        info = default!;
        if (props.Metadata is not { Count: > 0 } meta) return false;
        if (!meta.TryGetValue(LockIdKey, out var lockId) || string.IsNullOrEmpty(lockId)) return false;
        if (!meta.TryGetValue(CreatedKey, out var createdStr)
            || !DateTimeOffset.TryParse(createdStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var created))
        {
            return false;
        }
        info = new WopiLockInfo { FileId = fileId, LockId = lockId, DateCreated = created };
        return true;
    }
}
