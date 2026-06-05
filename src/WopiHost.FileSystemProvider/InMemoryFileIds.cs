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
/// <see cref="WasScanned"/>) are lock-free. Mutations (<see cref="AddFile"/> /
/// <see cref="UpdateFile"/> / <see cref="RemoveId"/> / <see cref="ScanAll"/>) serialize on a
/// private lock to keep the two maps consistent across the multi-step rebinding sequence
/// (clear stale reverse entry → install new forward entry → install new reverse entry).
/// </para>
/// </remarks>
public partial class InMemoryFileIds(ILogger<InMemoryFileIds> logger)
{
    private readonly ConcurrentDictionary<string, string> _idToPath = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _pathToId = new(StringComparer.Ordinal);
    private readonly Lock _writeLock = new();

    /// <summary>
    /// Gets a value indicating whether any files have been scanned.
    /// </summary>
    public bool WasScanned => !_idToPath.IsEmpty;

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
        var id = IdFromPath(path);
        lock (_writeLock)
        {
            SetMapping(id, path);
        }
        return id;
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
                // Drop the reverse entry only if it still points at this id — otherwise this would
                // clobber a newer binding that re-attached the same path to a different id.
                ((ICollection<KeyValuePair<string, string>>)_pathToId)
                    .Remove(new KeyValuePair<string, string>(path, fileId));
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

            SetMapping(IdFromPath(rootPath), rootPath);

            foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories))
            {
                SetMapping(IdFromPath(directory), directory);
            }

            foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
            {
                var newId = file.EndsWith("test.wopitest", StringComparison.OrdinalIgnoreCase)
                    ? "WOPITEST"
                    : IdFromPath(file);
                SetMapping(newId, file);
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
            ((ICollection<KeyValuePair<string, string>>)_pathToId)
                .Remove(new KeyValuePair<string, string>(oldPath, id));
        }
        _idToPath[id] = path;
        _pathToId[path] = id;
    }

    /// <summary>
    /// Creates a deterministic identifier from a file path so that the same path always
    /// produces the same identifier, even across process restarts or separate services.
    /// </summary>
    /// <remarks>
    /// Case-folds with <see cref="string.ToUpperInvariant"/> before delegating to
    /// <see cref="WopiResourceId.FromCanonicalPath"/>: Windows and macOS filesystems compare
    /// names case-insensitively, so two casings of the same file must produce one stable id.
    /// On case-sensitive Linux filesystems this is conservative but harmless — the same path
    /// still hashes to the same id.
    /// </remarks>
    private static string IdFromPath(string path) =>
        WopiResourceId.FromCanonicalPath(Path.GetFullPath(path).ToUpperInvariant());
}
