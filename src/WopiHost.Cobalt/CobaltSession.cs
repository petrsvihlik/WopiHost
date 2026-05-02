using System.Collections.Concurrent;
using System.Security.Claims;
using Cobalt;
using Cobalt.Base.IO;
using WopiHost.Abstractions;

namespace WopiHost.Cobalt;

/// <summary>
/// MS-FSSHTTP / Cobalt processor that maintains a long-lived <see cref="CobaltFile"/>
/// per WOPI file id.
/// </summary>
/// <remarks>
/// <para>
/// The Cobalt protocol is stateful: schema/exclusive locks, edit deltas, and
/// co-authoring metadata live inside the <c>CobaltFile</c>'s in-memory blob
/// stores and must persist across HTTP requests for the same file. Creating a
/// fresh <c>CobaltFile</c> per request (the previous behavior) makes
/// co-authoring and delta-based edits impossible.
/// </para>
/// <para>
/// Sessions are cached in a <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// keyed by <see cref="IWopiFile.Identifier"/>. A periodic timer evicts idle
/// sessions after <see cref="SessionIdleTimeout"/> to keep memory bounded —
/// the <see cref="LocalHostBlobStore"/> backing each session is in-memory.
/// </para>
/// </remarks>
public sealed class CobaltProcessor : ICobaltProcessor, IDisposable
{
    /// <summary>How long a session may go without traffic before it is disposed.</summary>
    public static TimeSpan SessionIdleTimeout { get; } = TimeSpan.FromMinutes(60);

    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    private readonly CoauthoringSessionTracker _sessionTracker = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<CobaltSessionEntry>>> _sessions = new(StringComparer.Ordinal);
    private readonly Timer _cleanupTimer;
    private int _disposed;

    public CobaltProcessor()
    {
        _cleanupTimer = new Timer(_ => EvictIdleSessions(), state: null, CleanupInterval, CleanupInterval);
    }

    /// <inheritdoc/>
    public async Task<byte[]> ProcessCobalt(IWopiFile file, ClaimsPrincipal principal, byte[] newContent)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(newContent);

        var entry = await GetOrCreateSession(file).ConfigureAwait(false);

        // Wrap the request bytes as a CobaltStream (16.x replaced the Atom-taking
        // overload of DeserializeInputFromProtocol with one that takes CobaltStream).
        using var newContentStream = new MemoryStream(newContent, writable: false);
        var requestStream = CobaltStream.Get(newContentStream, streamIsImmutable: true);
        var requestBatch = new RequestBatch();
        requestBatch.DeserializeInputFromProtocol(requestStream, out _, out var protocolVersion);

        // Flow the current principal to CobaltHostLockingStore.HandleWhoAmI for the
        // duration of this request only. AsyncLocal flows through async/await but is
        // scoped per logical-call so concurrent requests for the same file from
        // different users don't cross-pollute.
        var prev = CobaltHostLockingStore.CurrentPrincipal.Value;
        CobaltHostLockingStore.CurrentPrincipal.Value = principal;
        byte[] response;
        try
        {
            entry.Touch();
            entry.File.CobaltEndpoint.ExecuteRequestBatch(requestBatch);

            if (requestBatch.Requests.Any(r => r is PutChangesRequest && r.PartitionId == FilePartitionId.Content))
            {
                // Serialize concurrent saves for the same file. WOPI itself uses
                // protocol-level locks but those don't necessarily cover the
                // window between ExecuteRequestBatch and the disk flush.
                await entry.WriteLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    using var stream = await file.GetWriteStream().ConfigureAwait(false);
                    new GenericFda(entry.File.CobaltEndpoint).GetContentStream().CopyTo(stream);
                }
                finally
                {
                    entry.WriteLock.Release();
                }
            }

            using var ms = new MemoryStream();
            requestBatch.SerializeOutputToProtocol(protocolVersion).CopyTo(ms);
            response = ms.ToArray();
        }
        finally
        {
            CobaltHostLockingStore.CurrentPrincipal.Value = prev;
        }

        return response;
    }

    private async Task<CobaltSessionEntry> GetOrCreateSession(IWopiFile file)
    {
        var lazy = _sessions.GetOrAdd(
            file.Identifier,
            _ => new Lazy<Task<CobaltSessionEntry>>(() => CreateSessionEntry(file), LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await lazy.Value.ConfigureAwait(false);
        }
        catch
        {
            // A faulted Lazy<Task<>> would otherwise be cached forever; remove
            // exactly this entry so the next call retries. The KeyValuePair
            // overload of TryRemove avoids racing with a concurrent
            // GetOrCreateSession that already replaced the entry.
            _sessions.TryRemove(new KeyValuePair<string, Lazy<Task<CobaltSessionEntry>>>(file.Identifier, lazy));
            throw;
        }
    }

    private async Task<CobaltSessionEntry> CreateSessionEntry(IWopiFile file)
    {
        var disposal = new DisposalEscrow(file.Owner);

        // CobaltCore 16.x retired `TemporaryHostBlobStore`; `LocalHostBlobStore` is
        // the closest equivalent (in-memory by default; can be backed by a
        // directory if a `dirPathForFileBackedBlobs` is supplied — left null here
        // to match the old temp-only behavior).
        CobaltFilePartitionConfig MakePartition(FilePartitionId partition, bool genericFda) => new()
        {
            IsNewFile = true,
            HostBlobStore = new LocalHostBlobStore(new LocalHostBlobStore.Config()),
            cellSchemaIsGenericFda = genericFda,
            CellStorageConfig = new CellStorageConfig(),
            Schema = CobaltFilePartition.Schema.ShreddedCobalt,
            PartitionId = partition,
        };

        var partitionConfigs = new Dictionary<FilePartitionId, CobaltFilePartitionConfig>
        {
            [FilePartitionId.Content] = MakePartition(FilePartitionId.Content, genericFda: true),
            [FilePartitionId.WordWacUpdate] = MakePartition(FilePartitionId.WordWacUpdate, genericFda: false),
            [FilePartitionId.CoauthMetadata] = MakePartition(FilePartitionId.CoauthMetadata, genericFda: false),
        };

        // CobaltCore 16.x changed the CobaltFile ctor to take a
        // `GetConfigForPartitionAndVersion` delegate instead of a Dictionary.
        var cobaltFile = new CobaltFile(
            disposal,
            (partition, _) => partitionConfigs.TryGetValue(partition, out var cfg) ? cfg : null,
            new CobaltHostLockingStore(file.Identifier, _sessionTracker),
            null);

        if (file.Exists)
        {
            using var stream = await file.GetReadStream().ConfigureAwait(false);
            // 16.x made `AtomFromStream` an internal sealed type; `Atom.CreateFromArray`
            // is the public byte[] factory. Buffer the stream so we can hand a byte[]
            // to it.
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            var srcAtom = Atom.CreateFromArray(ms.ToArray());
            cobaltFile.GetCobaltFilePartition(FilePartitionId.Content).SetStream(RootId.Default.Value, srcAtom, out _);
            cobaltFile.GetCobaltFilePartition(FilePartitionId.Content).GetStream(RootId.Default.Value).Flush();
        }

        return new CobaltSessionEntry(cobaltFile, disposal);
    }

    private void EvictIdleSessions()
    {
        if (Volatile.Read(ref _disposed) != 0) return;

        var cutoff = DateTimeOffset.UtcNow - SessionIdleTimeout;
        foreach (var pair in _sessions)
        {
            if (!pair.Value.IsValueCreated)
            {
                continue;
            }

            // The Lazy<Task<...>> may not have completed yet for a brand-new entry;
            // skip those — they're definitionally not idle.
            var task = pair.Value.Value;
            if (task.Status != TaskStatus.RanToCompletion)
            {
                continue;
            }

            var entry = task.Result;
            if (entry.LastUsed < cutoff && _sessions.TryRemove(pair.Key, out _))
            {
                entry.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _cleanupTimer.Dispose();
        foreach (var pair in _sessions)
        {
            if (pair.Value.IsValueCreated && pair.Value.Value.Status == TaskStatus.RanToCompletion)
            {
                pair.Value.Value.Result.Dispose();
            }
        }
        _sessions.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(CobaltProcessor));
    }

    private sealed class CobaltSessionEntry(CobaltFile file, DisposalEscrow disposal) : IDisposable
    {
        private long _lastUsedTicks = DateTimeOffset.UtcNow.UtcTicks;

        public CobaltFile File { get; } = file;
        public SemaphoreSlim WriteLock { get; } = new(initialCount: 1, maxCount: 1);
        public DateTimeOffset LastUsed => new(Volatile.Read(ref _lastUsedTicks), TimeSpan.Zero);

        public void Touch() => Volatile.Write(ref _lastUsedTicks, DateTimeOffset.UtcNow.UtcTicks);

        public void Dispose()
        {
            disposal.Dispose();
            WriteLock.Dispose();
        }
    }
}
