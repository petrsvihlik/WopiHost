using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Maps file and folder identifiers to their paths and back.
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
/// Identifiers derive deterministically from paths (see <see cref="WopiResourceId.FromCanonicalPath"/>),
/// so every process over the same tree computes the same id for the same path.
/// </para>
/// <para>
/// The map holds no filesystem-convergence logic of its own; keeping it in sync with the tree
/// (watcher events, reconciliation sweeps) is <see cref="FileIdMapSynchronizer"/>'s job.
/// </para>
/// </remarks>
/// <param name="logger">The logger.</param>
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
    /// Gets a value indicating whether the map holds any entry, whether from <see cref="ScanAll"/> or
    /// from an individual registration.
    /// </summary>
    public bool WasScanned => !_idToPath.IsEmpty;

    /// <summary>
    /// Gets the stored paths. A test hook: <see cref="WopiFileSystemProvider"/> enumerates stored paths
    /// directly, which is only safe while every stored path is absolute, and tests pin that invariant through this view.
    /// </summary>
    internal ICollection<string> StoredPaths => _idToPath.Values;

    /// <summary>
    /// Tries to get the identifier mapped to the specified path.
    /// </summary>
    /// <param name="path">The absolute path of the file or folder.</param>
    /// <param name="fileId">When this method returns <see langword="true"/>, the identifier mapped to <paramref name="path"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="path"/> is mapped; otherwise, <see langword="false"/>.</returns>
    public bool TryGetFileId(string path, [NotNullWhen(true)] out string? fileId)
        => _pathToId.TryGetValue(path, out fileId);

    /// <summary>
    /// Tries to get the path mapped to the specified identifier.
    /// </summary>
    /// <param name="fileId">The identifier of the file or folder.</param>
    /// <param name="path">When this method returns <see langword="true"/>, the absolute path mapped to <paramref name="fileId"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="fileId"/> is mapped; otherwise, <see langword="false"/>.</returns>
    public bool TryGetPath(string fileId, [NotNullWhen(true)] out string? path)
        => _idToPath.TryGetValue(fileId, out path);

    /// <summary>
    /// Gets the path mapped to the specified identifier.
    /// </summary>
    /// <param name="fileId">The identifier of the file or folder.</param>
    /// <returns>The absolute path, or <see langword="null"/> if <paramref name="fileId"/> is not mapped.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fileId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fileId"/> is empty.</exception>
    public string? GetPath(string fileId)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileId);
        return _idToPath.TryGetValue(fileId, out var path) ? path : null;
    }

    /// <summary>
    /// Registers a file or folder path under its derived identifier, rebinding the identifier if it
    /// was mapped to another path.
    /// </summary>
    /// <param name="path">The absolute path of the file or folder.</param>
    /// <returns>The identifier derived from <paramref name="path"/>.</returns>
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
    /// Gets the identifier for the specified path, deriving and registering it when the path
    /// is not yet mapped. Identifiers are deterministic, so a path that appeared after the
    /// startup scan — created or renamed by another process sharing the same tree — resolves to
    /// the same identifier in every process.
    /// </summary>
    /// <param name="path">The full path to look up or register.</param>
    /// <returns>The identifier mapped to <paramref name="path"/>.</returns>
    internal string GetOrAddFileId(string path)
        => TryGetFileId(path, out var fileId) ? fileId : AddFile(path);

    /// <summary>
    /// Installs <paramref name="id"/>→<paramref name="path"/> without displacing the path's
    /// existing reverse binding: a path that already holds an id (a rename that kept the original
    /// id alive) keeps it as canonical, and <paramref name="id"/> becomes an additional alias for
    /// the path.
    /// </summary>
    /// <param name="id">The identifier to bind.</param>
    /// <param name="path">The full path to bind it to.</param>
    /// <returns><see langword="true"/> if the mapping was installed; <see langword="false"/> if it was already present.</returns>
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
    /// <param name="path">The full path whose binding to remove.</param>
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
    /// binding the target path may have held); aliases stay aliases.
    /// </summary>
    /// <param name="oldPath">The full path before the rename.</param>
    /// <param name="newPath">The full path after the rename.</param>
    /// <returns><see langword="true"/> if at least one identifier was repointed; <see langword="false"/> if nothing was bound under <paramref name="oldPath"/>.</returns>
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
    /// Removes the specified identifier and its path binding.
    /// </summary>
    /// <param name="fileId">The identifier of the file or folder.</param>
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
    /// Binds the specified identifier to a new path, such as after a rename.
    /// </summary>
    /// <param name="id">The identifier of the file or folder.</param>
    /// <param name="path">The new absolute path.</param>
    public void UpdateFile(string id, string path)
    {
        lock (_writeLock)
        {
            SetMapping(id, path);
        }
    }

    /// <summary>
    /// Replaces the map with <paramref name="rootPath"/> and every file and directory beneath it.
    /// </summary>
    /// <param name="rootPath">The absolute path of the root folder.</param>
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
    /// path ending in <c>test.wopitest</c> gets <see cref="WopiTestFileId"/> no matter how it appeared.
    /// </summary>
    /// <remarks>
    /// Case-folds with <see cref="string.ToUpperInvariant"/> before delegating to
    /// <see cref="WopiResourceId.FromCanonicalPath"/>: Windows and macOS filesystems compare
    /// names case-insensitively, so two casings of the same file must produce one stable id.
    /// On case-sensitive Linux filesystems this is conservative but harmless — the same path
    /// still hashes to the same id.
    /// </remarks>
    /// <param name="path">The file or directory path to derive the identifier from.</param>
    /// <returns>The lower-case hex SHA-256 identifier for <paramref name="path"/>, or <see cref="WopiTestFileId"/> for any path ending in <c>test.wopitest</c>.</returns>
    internal static string DeriveId(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.EndsWith("test.wopitest", StringComparison.OrdinalIgnoreCase)
            ? WopiTestFileId
            : WopiResourceId.FromCanonicalPath(fullPath.ToUpperInvariant());
    }
}
