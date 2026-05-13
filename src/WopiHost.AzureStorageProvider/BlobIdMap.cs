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
/// </remarks>
public sealed partial class BlobIdMap(ILogger<BlobIdMap> logger)
{
    /// <summary>
    /// Zero-byte marker blob name used to materialize otherwise-empty folders.
    /// </summary>
    /// <remarks>
    /// Plain Azure Blob Storage has no concept of an empty directory. To keep parity with
    /// <c>WopiFileSystemProvider</c>'s ability to create an empty folder via
    /// <c>CreateWopiChildResource&lt;IWopiFolder&gt;</c>, we drop a 0-byte blob with this name into the
    /// folder. The provider hides marker blobs from listings so callers never see them.
    /// </remarks>
    public const string FolderMarker = ".wopi.folder";

    private readonly Dictionary<string, string> _idToPath = new(StringComparer.Ordinal);

    /// <summary>Whether <see cref="ScanAll(IEnumerable{string})"/> has been called.</summary>
    public bool WasScanned { get; private set; }

    /// <summary>Looks up the blob path for an identifier.</summary>
    public bool TryGetPath(string fileId, [NotNullWhen(true)] out string? path)
        => _idToPath.TryGetValue(fileId, out path);

    /// <summary>Looks up the identifier for a blob path. Path comparison is case-sensitive.</summary>
    public bool TryGetFileId(string path, [NotNullWhen(true)] out string? fileId)
    {
        foreach (var pair in _idToPath)
        {
            if (pair.Value == path)
            {
                fileId = pair.Key;
                return true;
            }
        }
        fileId = null;
        return false;
    }

    /// <summary>Adds (or replaces) a path-to-id mapping and returns the deterministic id.</summary>
    public string Add(string path)
    {
        var id = IdFromPath(path);
        _idToPath[id] = path;
        return id;
    }

    /// <summary>Removes a mapping by id.</summary>
    public bool Remove(string fileId) => _idToPath.Remove(fileId);

    /// <summary>Updates the path for an existing id (used after a rename to keep the id stable).</summary>
    public void Update(string fileId, string newPath) => _idToPath[fileId] = newPath;

    /// <summary>
    /// Rebuilds the map from a flat enumeration of blob paths. The empty path is treated as the root
    /// container. Every directory prefix encountered is also added so containers without an explicit
    /// folder-marker blob are still addressable.
    /// </summary>
    public void ScanAll(IEnumerable<string> blobPaths)
    {
        _idToPath.Clear();
        // Root container is always identified by the empty string.
        _idToPath[IdFromPath(string.Empty)] = string.Empty;

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

            _idToPath[IdFromPath(path)] = path;

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
            _idToPath[IdFromPath(current)] = current;
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
