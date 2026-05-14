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
    public Task<bool> RefreshLockAsync(string fileId, string expectedExistingLockId, CancellationToken cancellationToken = default)
        => TryAtomicLockUpdateAsync(fileId, expectedExistingLockId, BumpCreatedTimestamp, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryUnlockAndRelockAsync(string fileId, string newLockId, string expectedExistingLockId, CancellationToken cancellationToken = default)
        => TryAtomicLockUpdateAsync(fileId, expectedExistingLockId, m => SwapLockIdAndBumpTimestamp(m, newLockId), cancellationToken);

    private void BumpCreatedTimestamp(Dictionary<string, string> metadata)
        => metadata[CreatedKey] = _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);

    private void SwapLockIdAndBumpTimestamp(Dictionary<string, string> metadata, string newLockId)
    {
        metadata[LockIdKey] = newLockId;
        BumpCreatedTimestamp(metadata);
    }

    /// <summary>
    /// Shared "load → validate → renew lease → SetMetadata under ETag" flow that
    /// <see cref="RefreshLockAsync"/> and <see cref="TryUnlockAndRelockAsync"/> both need. They
    /// differ only in <em>which</em> metadata fields the mutation step writes; the load /
    /// expected-lock-id check / lease renewal / ETag-conditional write / failure-handling
    /// scaffolding is identical, so it lives here as a single transaction instead of being
    /// duplicated across both methods.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Atomicity: the ETag captured before lease renewal is the snapshot the
    /// <c>SetMetadataAsync</c> writes against (<c>IfMatch=etag</c>). Any concurrent mutation
    /// between our load and our write — a sibling <c>UnlockAndRelock</c>, a <c>Remove</c>, even
    /// another <c>Refresh</c> — changes the blob's ETag and Azure replies 412; we report
    /// not-applied. Same atomicity guarantee in both call sites.
    /// </para>
    /// <para>
    /// Returns <see langword="false"/> on any abort condition: blob missing, malformed
    /// metadata, expired record, lock-id mismatch, missing or stale Azure lease, ETag race, or
    /// any other <see cref="RequestFailedException"/> on the SetMetadata write.
    /// </para>
    /// </remarks>
    private async Task<bool> TryAtomicLockUpdateAsync(
        string fileId,
        string expectedExistingLockId,
        Action<Dictionary<string, string>> mutateMetadata,
        CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);
        var blobClient = GetLockBlob(fileId);
        var props = await TryGetPropertiesAsync(blobClient, cancellationToken).ConfigureAwait(false);
        // Three short-circuiting guards collapsed into one if/else-if/else chain that assigns a
        // single `result` and exits through one `return`. The earlier multi-return shape tripped
        // qlty's return-count threshold (≤5); the combined-if shape tripped its boolean-logic
        // threshold. The if/else-if/else preserves the short-circuit semantics with no compound
        // boolean (each branch carries at most one `||`).
        bool result;
        if (props is null || !TryReadLock(fileId, props, out var info))
        {
            result = false;
        }
        else if (info.IsExpiredAt(_timeProvider.GetUtcNow()) || !_lockComparer.AreEqual(info.LockId, expectedExistingLockId))
        {
            result = false;
        }
        else if (!props.Metadata.TryGetValue(LeaseIdKey, out var leaseId) || string.IsNullOrEmpty(leaseId))
        {
            result = false;
        }
        else
        {
            var context = new MutationContext(blobClient, props, leaseId, fileId);
            result = await ExecuteMutationAsync(context, mutateMetadata, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    /// <summary>
    /// Bundled "what to write to" inputs for <see cref="ExecuteMutationAsync"/>. Exists so that
    /// helper can stay below qlty's 5-parameter threshold; passing four positional args plus the
    /// mutation delegate and the cancellation token would otherwise put it at 6.
    /// </summary>
    private readonly record struct MutationContext(
        BlobClient BlobClient,
        BlobProperties Props,
        string LeaseId,
        string FileId);

    /// <summary>
    /// Inner step of <see cref="TryAtomicLockUpdateAsync"/>: with a validated lease id in hand,
    /// renews the lease and writes the mutated metadata under <c>IfMatch=etag</c> + the lease id.
    /// Lifted out so the outer validation path and the renew/write path each have a single
    /// return — qlty's return-count threshold is ≤5, and the combined version exceeded it.
    /// </summary>
    /// <remarks>
    /// A <c>renewed</c> flag tracks which phase the exception fired in so the two failure modes
    /// stay distinguishable for telemetry: <see cref="LogLeaseRenewFailed"/> means the existing
    /// lease was externally broken (bad state); <see cref="LogLockMetadataUpdateFailed"/> means
    /// the SetMetadata write failed (typically 412 = concurrent mutation, 409 = lease conflict).
    /// </remarks>
    private async Task<bool> ExecuteMutationAsync(
        MutationContext context,
        Action<Dictionary<string, string>> mutateMetadata,
        CancellationToken cancellationToken)
    {
        bool result;
        var leaseClient = context.BlobClient.GetBlobLeaseClient(context.LeaseId);
        var renewed = false;
        try
        {
            await leaseClient.RenewAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            renewed = true;
            var updated = new Dictionary<string, string>(context.Props.Metadata, StringComparer.Ordinal);
            mutateMetadata(updated);
            await context.BlobClient.SetMetadataAsync(updated,
                conditions: new BlobRequestConditions { LeaseId = context.LeaseId, IfMatch = context.Props.ETag },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            result = true;
        }
        catch (RequestFailedException ex)
        {
            if (renewed)
            {
                LogLockMetadataUpdateFailed(_logger, ex, context.FileId);
            }
            else
            {
                LogLeaseRenewFailed(_logger, ex, context.FileId);
            }
            result = false;
        }
        return result;
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
