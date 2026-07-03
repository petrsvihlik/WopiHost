using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Provides unique file identifiers for files.
/// </summary>
/// <remarks>
/// <para>
/// Very basic in-memory store for sample purposes only.
/// </para>
/// <para>
/// Backed by two <see cref="ConcurrentDictionary{TKey, TValue}"/> instances — the primary
/// id→path map plus a path→id reverse-lookup map — so <see cref="TryGetFileId"/> is O(1) rather
/// than an O(n) scan. Reads
/// (<see cref="TryGetFileId"/> / <see cref="TryGetPath"/> / <see cref="GetPath"/> /
/// <see cref="WasScanned"/>) are lock-free. Mutations serialize on a private lock to keep the
/// two maps consistent across the multi-step rebinding sequence
/// (clear stale reverse entry → install new forward entry → install new reverse entry).
/// </para>
/// <para>
/// The map holds no filesystem-convergence logic of its own; keeping it in sync with the tree
/// (watcher events, reconciliation sweeps) is <see cref="FileIdMapSynchronizer"/>'s job.
/// </para>
/// </remarks>
public partial class InMemoryFileIds(ILogger<InMemoryFileIds> logger)
{
    /// <summary>
    /// Fixed identifier for the WOPI validator's <c>test.wopitest</c> file, so validator runs can
    /// address it without knowing the hash of its path.
    /// </summary>
    internal const string WopiTestFileId = "WOPITEST";

    private readonly ConcurrentDictionary<string, string> _idToPath = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _pathToId = new(StringComparer.Ordinal);
    private readonly Lock _writeLock = new();

    /// <summary>
    /// Gets a value indicating whether any files have been scanned.
    /// </summary>
    public bool WasScanned => !_idToPath.IsEmpty;

    // Test hook: WopiFileSystemProvider enumerates stored paths directly, which is only safe
    // while every stored path is absolute — tests pin that invariant through this view.
    internal ICollection<string> StoredPaths => _idToPath.Values;

    /// <summary>
    /// Gets the file identifier for the specified path.
    /// </summary>
    public bool TryGetFileId(string path, [NotNullWhen(true)] out string? fileId)
        => _pathToId.TryGetValue(path, out fileId);

    /// <summary>
    /// Gets the file path for the specified file identifier.
    /// </summary>
    public bool TryGetPath(string fileId, [NotNullWhen(true)] out string? path)
        => _idToPath.TryGetValue(fileId, out path);

    /// <summary>
    /// Gets the path for the specified file identifier.
    /// </summary>
    public string? GetPath(string fileId)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileId);
        return _idToPath.TryGetValue(fileId, out var path) ? path : null;
    }

    /// <summary>
    /// Adds a file to the collection and returns its identifier.
    /// </summary>
    public string AddFile(string path)
    {
        var id = DeriveId(path);
        lock (_writeLock)
        {
            SetMapping(id, path);
        }
        return id;
    }

    /// <summary>
    /// Returns the identifier for the specified path, deriving and registering it when the path
    /// is not yet mapped. Identifiers are deterministic, so a path that appeared after the
    /// startup scan — created or renamed by another process sharing the same tree — resolves to
    /// the same identifier in every process.
    /// </summary>
    internal string GetOrAddFileId(string path)
        => TryGetFileId(path, out var fileId) ? fileId : AddFile(path);

    /// <summary>
    /// Installs <paramref name="id"/>→<paramref name="path"/> without displacing the path's
    /// existing reverse binding: a path that already holds an id (a rename that kept the original
    /// id alive) keeps it as canonical, and <paramref name="id"/> becomes an additional alias for
    /// the path. Returns <see langword="false"/> when the exact mapping was already present.
    /// </summary>
    internal bool EnsureMapping(string id, string path)
    {
        lock (_writeLock)
        {
            if (_idToPath.TryGetValue(id, out var existing))
            {
                if (string.Equals(existing, path, StringComparison.Ordinal))
                {
                    return false;
                }
                RemoveReverseEntry(existing, id);
            }
            _idToPath[id] = path;
            _pathToId.TryAdd(path, id);
            return true;
        }
    }

    /// <summary>
    /// Removes the binding for the specified path, if any. Alias identifiers still pointing at
    /// the path are left in place — they resolve to a path that no longer exists, which callers
    /// already treat as missing — so the removal stays O(1) even when deletions arrive in bursts.
    /// </summary>
    internal void TryRemovePath(string path)
    {
        lock (_writeLock)
        {
            if (_pathToId.TryRemove(path, out var id))
            {
                // Drop the forward entry only if it still points at this path — otherwise this
                // would clobber an id that was already rebound elsewhere.
                ((ICollection<KeyValuePair<string, string>>)_idToPath)
                    .Remove(new KeyValuePair<string, string>(id, path));
            }
        }
    }

    /// <summary>
    /// Repoints every identifier bound to <paramref name="oldPath"/> — or to an entry beneath it
    /// — onto the corresponding path under <paramref name="newPath"/>, preserving each id across
    /// the rename. Canonical bindings stay canonical (a witnessed rename outranks any stale
    /// binding the target path may have held); aliases stay aliases. Returns
    /// <see langword="false"/> when nothing was bound under <paramref name="oldPath"/>.
    /// </summary>
    internal bool RepointSubtree(string oldPath, string newPath)
    {
        lock (_writeLock)
        {
            var repointed = false;
            var prefix = oldPath + Path.DirectorySeparatorChar;
            // ConcurrentDictionary enumeration is safe under concurrent mutation and visits each
            // key once; only values of already-visited keys are rewritten here.
            foreach (var pair in _idToPath)
            {
                string updated;
                if (string.Equals(pair.Value, oldPath, StringComparison.Ordinal))
                {
                    updated = newPath;
                }
                else if (pair.Value.StartsWith(prefix, StringComparison.Ordinal))
                {
                    updated = string.Concat(newPath, pair.Value.AsSpan(oldPath.Length));
                }
                else
                {
                    continue;
                }

                var wasCanonical = _pathToId.TryGetValue(pair.Value, out var canonicalId)
                    && string.Equals(canonicalId, pair.Key, StringComparison.Ordinal);
                if (wasCanonical)
                {
                    RemoveReverseEntry(pair.Value, pair.Key);
                    _pathToId[updated] = pair.Key;
                }
                else
                {
                    _pathToId.TryAdd(updated, pair.Key);
                }
                _idToPath[pair.Key] = updated;
                repointed = true;
            }
            return repointed;
        }
    }

    /// <summary>
    /// Removes the specified file identifier.
    /// </summary>
    public void RemoveId(string fileId)
    {
        lock (_writeLock)
        {
            if (_idToPath.TryRemove(fileId, out var path))
            {
                RemoveReverseEntry(path, fileId);
            }
        }
    }

    /// <summary>
    /// Updates the file path for the specified file identifier.
    /// </summary>
    public void UpdateFile(string id, string path)
    {
        lock (_writeLock)
        {
            SetMapping(id, path);
        }
    }

    /// <summary>
    /// Scans all files and directories in the specified root path.
    /// </summary>
    public void ScanAll(string rootPath)
    {
        lock (_writeLock)
        {
            _idToPath.Clear();
            _pathToId.Clear();

            foreach (var entry in FileSystemEnumeration.EnumerateTree(rootPath))
            {
                SetMapping(DeriveId(entry), entry);
            }

            LogScannedItems(logger, _idToPath.Count);
        }
    }

    /// <summary>
    /// Installs or rebinds the id↔path pair, keeping both maps consistent. The caller must
    /// hold <see cref="_writeLock"/> — the three steps (drop stale reverse entry, install
    /// forward entry, install reverse entry) are not individually atomic.
    /// </summary>
    private void SetMapping(string id, string path)
    {
        if (_idToPath.TryGetValue(id, out var oldPath) && !string.Equals(oldPath, path, StringComparison.Ordinal))
        {
            RemoveReverseEntry(oldPath, id);
        }
        _idToPath[id] = path;
        _pathToId[path] = id;
    }

    /// <summary>
    /// Drops the reverse entry only if it still points at this id — otherwise this would
    /// clobber a newer binding that re-attached the same path to a different id.
    /// </summary>
    private void RemoveReverseEntry(string path, string id)
        => ((ICollection<KeyValuePair<string, string>>)_pathToId)
            .Remove(new KeyValuePair<string, string>(path, id));

    /// <summary>
    /// Creates a deterministic identifier from a file path so that the same path always
    /// produces the same identifier, even across process restarts or separate services.
    /// This is the single derivation policy — every flow that mints an id (startup scan,
    /// create, lazy registration, watcher event, reconciliation sweep) goes through it, so a
    /// <c>test.wopitest</c> file gets <see cref="WopiTestFileId"/> no matter how it appeared.
    /// </summary>
    /// <remarks>
    /// Case-folds with <see cref="string.ToUpperInvariant"/> before delegating to
    /// <see cref="WopiResourceId.FromCanonicalPath"/>: Windows and macOS filesystems compare
    /// names case-insensitively, so two casings of the same file must produce one stable id.
    /// On case-sensitive Linux filesystems this is conservative but harmless — the same path
    /// still hashes to the same id.
    /// </remarks>
    internal static string DeriveId(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.EndsWith("test.wopitest", StringComparison.OrdinalIgnoreCase)
            ? WopiTestFileId
            : WopiResourceId.FromCanonicalPath(fullPath.ToUpperInvariant());
    }
}
