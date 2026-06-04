using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Maps WOPI identifiers to blob paths inside a single Azure Blob container.
/// </summary>
/// <remarks>
/// <para>
/// Identifiers are deterministic hex-SHA-256 hashes of the blob path (see
/// <see cref="WopiResourceId.FromCanonicalPath"/>) so the same blob always produces the same id
/// across process restarts and separate WopiHost instances. The mapping itself is held in memory
/// and is rebuilt by <see cref="ScanAll"/> at startup; this matches the pattern used by
/// <c>WopiHost.FileSystemProvider</c>'s <c>InMemoryFileIds</c>.
/// </para>
/// <para>
/// <strong>Blob paths are hashed as-is, without case-folding.</strong> Azure Blob Storage treats
/// blob names case-sensitively — <c>Folder/file.txt</c> and <c>folder/file.txt</c> are distinct
/// blobs — so case-folding before hashing would silently merge two distinct blobs onto a single
/// id, with the second's mapping overwriting the first in the in-memory dictionary.
/// </para>
/// <para>
/// Folders are virtual: a path with no extension and no marker blob still appears in the map if any
/// child blob shares its prefix, plus the explicit folder-marker blob (<see cref="FolderMarker"/>) is
/// recognised as creating an otherwise-empty folder.
/// </para>
/// <para>
/// <strong>Two dictionaries, kept in sync.</strong> <c>_idToPath</c> drives id→path lookups
/// (which fire on every WOPI route that resolves <c>{id}</c>); <c>_pathToId</c> is the inverse
/// for the listing path, where the provider walks the blob namespace and needs to look up the id
/// for each blob name. The reverse dictionary keeps that lookup O(1); without it the listing
/// hydration loop would degenerate to O(n^2) in containers with thousands of blobs.
/// </para>
/// </remarks>
public sealed partial class BlobIdMap(ILogger<BlobIdMap> logger)
{
    /// <summary>
    /// Zero-byte marker blob name used to materialize otherwise-empty folders.
    /// </summary>
    /// <remarks>
    /// Plain Azure Blob Storage has no concept of an empty directory. To keep parity with
    /// <c>WopiFileSystemProvider</c>'s ability to create an empty folder via
    /// <see cref="IWopiWritableStorageProvider.CreateWopiChildContainer"/>, a 0-byte blob with
    /// this name is dropped into the folder. The provider hides marker blobs from listings so
    /// callers never see them.
    /// </remarks>
    public const string FolderMarker = ".wopi.folder";

    private readonly Dictionary<string, string> _idToPath = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _pathToId = new(StringComparer.Ordinal);

    /// <summary>Whether <see cref="ScanAll(IEnumerable{string})"/> has been called.</summary>
    public bool WasScanned { get; private set; }

    /// <summary>Looks up the blob path for an identifier.</summary>
    public bool TryGetPath(string fileId, [NotNullWhen(true)] out string? path)
        => _idToPath.TryGetValue(fileId, out path);

    /// <summary>Looks up the identifier for a blob path. Path comparison is case-sensitive.</summary>
    public bool TryGetFileId(string path, [NotNullWhen(true)] out string? fileId)
        => _pathToId.TryGetValue(path, out fileId);

    /// <summary>Adds (or replaces) a path-to-id mapping and returns the deterministic id.</summary>
    public string Add(string path)
    {
        var id = IdFromPath(path);
        // Two writes that must stay in lockstep. If the id was previously mapped to a DIFFERENT
        // path (theoretically possible only when path strings collide on SHA-256 — practically
        // impossible) the old reverse entry would dangle, but the forward write below would
        // still rebind it. The realistic case "same id, same path" no-ops both writes.
        _idToPath[id] = path;
        _pathToId[path] = id;
        return id;
    }

    /// <summary>Removes a mapping by id.</summary>
    public bool Remove(string fileId)
    {
        if (!_idToPath.Remove(fileId, out var path))
        {
            return false;
        }
        _ = _pathToId.Remove(path);
        return true;
    }

    /// <summary>Updates the path for an existing id (used after a rename to keep the id stable).</summary>
    public void Update(string fileId, string newPath)
    {
        // Drop the old reverse entry first so a rename that moves blob A → blob B doesn't leave
        // _pathToId[oldPath]=A behind. If the id wasn't previously known, there's no reverse
        // entry to drop — the forward write below registers the new mapping fresh.
        if (_idToPath.TryGetValue(fileId, out var oldPath) && !string.Equals(oldPath, newPath, StringComparison.Ordinal))
        {
            _ = _pathToId.Remove(oldPath);
        }
        _idToPath[fileId] = newPath;
        _pathToId[newPath] = fileId;
    }

    /// <summary>
    /// Rebuilds the map from a flat enumeration of blob paths. The empty path is treated as the root
    /// container. Every directory prefix encountered is also added so containers without an explicit
    /// folder-marker blob are still addressable.
    /// </summary>
    public void ScanAll(IEnumerable<string> blobPaths)
    {
        _idToPath.Clear();
        _pathToId.Clear();
        // Root container is always identified by the empty string.
        var rootId = IdFromPath(string.Empty);
        _idToPath[rootId] = string.Empty;
        _pathToId[string.Empty] = rootId;

        var seenDirs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in blobPaths)
        {
            // Hide folder markers from the file map but make sure the parent folder is registered.
            var fileName = path[(path.LastIndexOf('/') + 1)..];
            if (fileName == FolderMarker)
            {
                var parent = path[..^FolderMarker.Length].TrimEnd('/');
                AddDirectoryAndAncestors(parent, seenDirs);
                continue;
            }

            var id = IdFromPath(path);
            _idToPath[id] = path;
            _pathToId[path] = id;

            var lastSlash = path.LastIndexOf('/');
            if (lastSlash > 0)
            {
                AddDirectoryAndAncestors(path[..lastSlash], seenDirs);
            }
        }

        WasScanned = true;
        LogScannedEntries(logger, _idToPath.Count);
    }

    private void AddDirectoryAndAncestors(string dirPath, HashSet<string> seen)
    {
        var current = dirPath;
        while (!string.IsNullOrEmpty(current) && seen.Add(current))
        {
            var id = IdFromPath(current);
            _idToPath[id] = current;
            _pathToId[current] = id;
            var slash = current.LastIndexOf('/');
            current = slash > 0 ? current[..slash] : string.Empty;
        }
    }

    /// <summary>
    /// Returns the deterministic id for a blob path. The path is hashed as-is — Azure blob
    /// names are case-sensitive, so two casings of the same name are <em>different</em> blobs
    /// and must produce different ids.
    /// </summary>
    public static string IdFromPath(string path) => WopiResourceId.FromCanonicalPath(path);
}
