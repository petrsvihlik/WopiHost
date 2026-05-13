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
/// <param name="containerClient">Blob container that holds the per-fileId lock placeholders.</param>
/// <param name="logger">Logger.</param>
/// <param name="timeProvider">
/// Clock source for lock timestamps and expiry. Defaults to <see cref="TimeProvider.System"/>
/// when not supplied via DI; inject a <c>FakeTimeProvider</c> (or any custom
/// <see cref="TimeProvider"/>) in tests to make expiry deterministic.
/// </param>
/// <param name="lockComparer">
/// Lock-id comparer. Defaults to <see cref="OrdinalWopiLockComparer"/> when not supplied
/// via DI; replace with a custom comparer (e.g. <see cref="JsonShapedWopiLockComparer"/>)
/// to absorb known WOPI-client lock-id mutations.
/// </param>
public partial class WopiAzureLockProvider(
    BlobContainerClient containerClient,
    ILogger<WopiAzureLockProvider> logger,
    TimeProvider? timeProvider = null,
    IWopiLockComparer? lockComparer = null) : IWopiLockProvider
{
    internal const string LockIdKey = "wopi_lock_id";
    internal const string LeaseIdKey = "wopi_lease_id";
    internal const string CreatedKey = "wopi_created";

    private readonly BlobContainerClient _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
    private readonly ILogger<WopiAzureLockProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IWopiLockComparer _lockComparer = lockComparer ?? OrdinalWopiLockComparer.Instance;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

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
        if (!info.IsExpiredAt(_timeProvider.GetUtcNow()))
        {
            return info;
        }

        // WOPI-expired. Break the lease so subsequent AddLock calls can take over, then evict.
        await TryBreakLeaseAsync(blobClient, cancellationToken).ConfigureAwait(false);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        LogLockExpired(_logger, fileId, info.LockId);
        return null;
    }

    /// <inheritdoc />
    public async Task<WopiLockInfo?> AddLockAsync(string fileId, string lockId, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);
        var blobClient = GetLockBlob(fileId);

        // Step 1: probe existing state.
        var existing = await TryGetPropertiesAsync(blobClient, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (TryReadLock(fileId, existing, out var existingInfo))
            {
                if (!existingInfo.IsExpiredAt(_timeProvider.GetUtcNow()))
                {
                    LogLockAddRejected(_logger, fileId, lockId, existingInfo.LockId);
                    return null;
                }
                // Stale (>30 min old) — clean up so we can take over.
                await TryBreakLeaseAsync(blobClient, cancellationToken).ConfigureAwait(false);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Blob exists but metadata is missing/malformed. With the atomic upload-with-
                // metadata flow below, healthy peers never observe this state — it can only mean
                // a peer is mid-acquire RIGHT NOW (we lost the race) or a previous attempt
                // crashed between Upload and lease acquisition. Either way, treat it as
                // "lock-attempt in progress" and yield. (Aggressive cleanup here was the prior
                // bug that broke healthy peers via lease-break + delete in their acquire window.)
                LogLockAddRaceLost(_logger, fileId, lockId);
                return null;
            }
        }

        // Step 2: atomic upload-with-metadata. The blob is never observable in a "no metadata"
        // state, so any peer's TryReadLock either gets a complete WopiLockInfo (we won) or the
        // blob doesn't yet exist (their probe hits before our upload lands). IfNoneMatch=*
        // serves as the create-if-not-exists conditional: a 412 means a sibling acquire raced
        // ahead and we lost. Some Azure SDK paths surface 409 instead — we treat both as the
        // race-lost outcome.
        var leaseId = Guid.NewGuid().ToString();
        var now = _timeProvider.GetUtcNow();
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LockIdKey] = lockId,
            [LeaseIdKey] = leaseId,
            [CreatedKey] = now.ToString("O", CultureInfo.InvariantCulture),
        };
        try
        {
            using var empty = new MemoryStream([], writable: false);
            await blobClient.UploadAsync(
                empty,
                new BlobUploadOptions
                {
                    Metadata = metadata,
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
        {
            LogLockAddRaceLost(_logger, fileId, lockId);
            return null;
        }

        // Step 3: acquire the infinite lease whose id is already announced via metadata. If
        // this fails, delete the placeholder so we don't leave a no-op blob behind that claims
        // a lease we don't actually hold.
        var leaseClient = blobClient.GetBlobLeaseClient(leaseId);
        try
        {
            await leaseClient.AcquireAsync(TimeSpan.FromSeconds(-1), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex)
        {
            LogLeaseAcquireFailed(_logger, ex, fileId);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return null;
        }

        LogLockAcquired(_logger, fileId, lockId);
        return new WopiLockInfo { FileId = fileId, LockId = lockId, DateCreated = now };
    }

    /// <inheritdoc />
    public async Task<bool> RefreshLockAsync(string fileId, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);
        var blobClient = GetLockBlob(fileId);
        var props = await TryGetPropertiesAsync(blobClient, cancellationToken).ConfigureAwait(false);
        if (props is null || !TryReadLock(fileId, props, out var info))
        {
            return false;
        }
        if (info.IsExpiredAt(_timeProvider.GetUtcNow()))
        {
            return false;
        }
        if (!props.Metadata.TryGetValue(LeaseIdKey, out var leaseId) || string.IsNullOrEmpty(leaseId))
        {
            return false;
        }

        // Renew the lease so the holder semantic stays alive (no-op for infinite leases but still
        // confirms the lease is still ours), then bump the WOPI-level timestamp.
        var leaseClient = blobClient.GetBlobLeaseClient(leaseId);
        try
        {
            await leaseClient.RenewAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex)
        {
            LogLeaseRenewFailed(_logger, ex, fileId);
            return false;
        }

        var updated = new Dictionary<string, string>(props.Metadata, StringComparer.Ordinal)
        {
            [CreatedKey] = _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
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
            LogLockMetadataUpdateFailed(_logger, ex, fileId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryUnlockAndRelockAsync(string fileId, string newLockId, string expectedExistingLockId, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);
        var blobClient = GetLockBlob(fileId);
        var props = await TryGetPropertiesAsync(blobClient, cancellationToken).ConfigureAwait(false);
        if (props is null || !TryReadLock(fileId, props, out var info))
        {
            return false;
        }
        if (info.IsExpiredAt(_timeProvider.GetUtcNow()) || !_lockComparer.AreEqual(info.LockId, expectedExistingLockId))
        {
            return false;
        }
        if (!props.Metadata.TryGetValue(LeaseIdKey, out var leaseId) || string.IsNullOrEmpty(leaseId))
        {
            return false;
        }

        // Snapshot the ETag at read time. If anything mutates the blob (another swap, refresh, or
        // remove) before our SetMetadata lands, the IfMatch condition fails with 412 and the swap
        // is correctly reported as not-applied.
        var etag = props.ETag;
        var leaseClient = blobClient.GetBlobLeaseClient(leaseId);
        try
        {
            await leaseClient.RenewAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex)
        {
            LogLeaseRenewFailed(_logger, ex, fileId);
            return false;
        }

        var updated = new Dictionary<string, string>(props.Metadata, StringComparer.Ordinal)
        {
            [LockIdKey] = newLockId,
            [CreatedKey] = _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
        };
        try
        {
            await blobClient.SetMetadataAsync(updated,
                conditions: new BlobRequestConditions { LeaseId = leaseId, IfMatch = etag },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 409)
        {
            // 412 = ETag changed (concurrent swap). 409 = lease conflict. Either way, swap aborted.
            LogLockMetadataUpdateFailed(_logger, ex, fileId);
            return false;
        }
        catch (RequestFailedException ex)
        {
            LogLockMetadataUpdateFailed(_logger, ex, fileId);
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
            LogLockRemoved(_logger, fileId, removedLockId);
        }
        return deleted.Value;
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }
        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }
            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private BlobClient GetLockBlob(string fileId)
    {
        // Hash the fileId so any caller-supplied identifier is reduced to a safe blob name.
        var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fileId))).ToLowerInvariant();
        return _containerClient.GetBlobClient(name);
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
