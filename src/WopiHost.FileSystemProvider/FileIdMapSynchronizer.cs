using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Keeps an <see cref="InMemoryFileIds"/> map converged with the directory tree it was built
/// from, so several processes sharing one tree resolve the same file under the same identifier.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="FileSystemWatcher"/> is the primary mechanism. Its rename event carries both
/// paths, so the existing identifier is repointed to the new path — the same id continuity the
/// renaming process gives its own map — instead of a fresh identifier being derived for what
/// looks like a new file. That keeps every process addressing one file through one id, which in
/// turn keeps WOPI locks on that file in a single domain.
/// </para>
/// <para>
/// Watcher delivery is asynchronous and lossy (fixed-size buffer; bursts overflow), and change
/// notifications are unavailable on some mounts entirely. <see cref="TryResolveByReconcile"/> is
/// the recovery path: a debounced full-tree sweep that registers the derived identifier of every
/// entry on disk. Enumeration and hashing run outside the map's write lock — map readers and
/// writers proceed while a sweep is in flight — and because one sweep registers everything, an
/// unresolvable identifier can never starve a legitimate one behind the debounce window.
/// </para>
/// </remarks>
internal sealed partial class FileIdMapSynchronizer : IDisposable
{
    private const long DefaultReconcileDebounceMs = 2_000;

    private static readonly SearchValues<char> s_lowerHex = SearchValues.Create("0123456789abcdef");

    private readonly InMemoryFileIds _fileIds;
    private readonly string _rootPath;
    private readonly ILogger _logger;
    private readonly long _reconcileDebounceMs;
    private readonly Lock _reconcileLock = new();
    // Guarded by _reconcileLock (the constructor's initial write happens-before any use).
    private long _lastReconcileAt;
    private FileSystemWatcher? _watcher;

    internal FileIdMapSynchronizer(
        InMemoryFileIds fileIds,
        string rootPath,
        ILogger logger,
        long reconcileDebounceMs = DefaultReconcileDebounceMs)
    {
        _fileIds = fileIds;
        _rootPath = rootPath;
        _logger = logger;
        _reconcileDebounceMs = reconcileDebounceMs;
        _lastReconcileAt = -reconcileDebounceMs;
    }

    /// <summary>
    /// Gets a value indicating whether change events are currently being observed.
    /// </summary>
    internal bool IsWatching => _watcher is not null;

    /// <summary>
    /// Starts observing the tree for changes. Failure is not fatal: change notifications are
    /// unreliable or unsupported on network shares and some container bind mounts, so the map
    /// degrades to lazy registration plus the reconciliation sweep and the failure is logged.
    /// </summary>
    internal void StartWatching()
    {
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(_rootPath)
            {
                IncludeSubdirectories = true,
                // Only name-affecting events matter to the id map; content writes are noise that
                // would just pressure the event buffer.
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                // The documented maximum. The 8 KB default overflows on bursts (a large tree
                // being moved in); overflow is recoverable via OnError but costs a full sweep.
                InternalBufferSize = 64 * 1024,
            };
            // One handler for all three: RenamedEventHandler accepts it through parameter
            // contravariance, and ApplyEvent dispatches on the event's ChangeType.
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnChanged;
            watcher.Error += OnError;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
            LogWatcherStarted(_logger, _rootPath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            watcher?.Dispose();
            LogWatcherStartFailed(_logger, _rootPath, ex);
        }
    }

    /// <summary>
    /// Attempts to resolve an identifier the map has never seen — minted by another process over
    /// the same tree after a change the watcher did not deliver — by sweeping the tree and
    /// registering the derived identifier of every entry.
    /// </summary>
    internal bool TryResolveByReconcile(string fileId, [NotNullWhen(true)] out string? path)
    {
        path = null;
        // Derived ids have exactly one shape (a 64-char lower-hex digest, or the fixed validator
        // id), so a malformed id can never be produced by hashing tree entries — reject it
        // without touching the disk.
        if (!IsDerivableId(fileId))
        {
            return false;
        }
        // Single flight: concurrent misses queue here and share the sweep the first one runs.
        lock (_reconcileLock)
        {
            if (_fileIds.TryGetPath(fileId, out path))
            {
                return true;
            }
            // A sweep registers every entry on disk, so within the debounce window a missing id
            // is genuinely unknown (or newer than the sweep — the watcher and the client's retry
            // cover that race); sweeping again would only re-hash the same tree.
            if (Environment.TickCount64 - _lastReconcileAt < _reconcileDebounceMs)
            {
                LogIdMiss(_logger, fileId, _rootPath);
                return false;
            }
            Reconcile();
            if (_fileIds.TryGetPath(fileId, out path))
            {
                LogIdResolvedByReconcile(_logger, fileId, path);
                return true;
            }
            LogIdMiss(_logger, fileId, _rootPath);
            return false;
        }
    }

    /// <summary>
    /// Registers the derived identifier of every entry currently on disk. Additive by design:
    /// existing path bindings are never displaced, so rename-retained identifiers stay canonical
    /// and live sessions survive the resync (a clear-and-rebuild would 404 them).
    /// The caller must hold <see cref="_reconcileLock"/>.
    /// </summary>
    private void Reconcile()
    {
        try
        {
            var registered = 0;
            foreach (var entry in FileSystemEnumeration.EnumerateTree(_rootPath))
            {
                // The id is derived before the map call, so hashing never runs under the map's
                // write lock — only the per-entry insertion does.
                registered += _fileIds.EnsureMapping(InMemoryFileIds.DeriveId(entry), entry) ? 1 : 0;
            }
            LogReconciled(_logger, _rootPath, registered);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // IgnoreInaccessible tolerates unreadable subtrees; this guards the root itself
            // vanishing or turning unreadable mid-sweep.
            LogReconcileFailed(_logger, _rootPath, ex);
        }
        finally
        {
            // A failed sweep is debounced too — a persistently broken root must not turn every
            // unresolved id into a full-tree walk.
            _lastReconcileAt = Environment.TickCount64;
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => ApplyEvent(e);

    /// <summary>
    /// Applies a watcher event to the map. A rename carries both paths, so the existing
    /// identifier is repointed — cross-process id continuity, exactly like the renaming
    /// process's own map update — with the whole subtree carried along for directory renames;
    /// when the old path was never mapped (an editor's safe-save temp file, for instance), the
    /// new path registers like a create. Internal so tests can drive it with synthetic events —
    /// real watcher callbacks are not raisable on demand.
    /// </summary>
    internal void ApplyEvent(FileSystemEventArgs e)
    {
        try
        {
            switch (e)
            {
                case RenamedEventArgs renamed:
                    if (!_fileIds.RepointSubtree(renamed.OldFullPath, renamed.FullPath))
                    {
                        _fileIds.GetOrAddFileId(renamed.FullPath);
                    }
                    break;
                case { ChangeType: WatcherChangeTypes.Created }:
                    _fileIds.GetOrAddFileId(e.FullPath);
                    break;
                case { ChangeType: WatcherChangeTypes.Deleted }:
                    _fileIds.TryRemovePath(e.FullPath);
                    break;
            }
        }
        catch (Exception ex)
        {
            // An exception escaping a watcher callback would take the process down; the map
            // self-heals through the reconciliation sweep, so log and move on.
            LogWatcherEventFailed(_logger, e.ChangeType, e.FullPath, ex);
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        LogWatcherError(_logger, _rootPath, e.GetException());
        ScheduleRecoverySweep();
    }

    /// <summary>
    /// Runs a debounced reconciliation sweep off the watcher's callback thread — events were
    /// lost (buffer overflow) or the watch itself is failing. The additive sweep restores
    /// anything the lost events would have registered; lost renames degrade to alias
    /// registration rather than invisibility. Internal so tests can drive it — an overflow is
    /// not provokable on demand.
    /// </summary>
    internal void ScheduleRecoverySweep()
        => _ = Task.Run(() =>
        {
            lock (_reconcileLock)
            {
                if (Environment.TickCount64 - _lastReconcileAt >= _reconcileDebounceMs)
                {
                    Reconcile();
                }
            }
        });

    private static bool IsDerivableId(string fileId)
        => string.Equals(fileId, InMemoryFileIds.WopiTestFileId, StringComparison.Ordinal)
            || (fileId.Length == 64 && !fileId.AsSpan().ContainsAnyExcept(s_lowerHex));

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
