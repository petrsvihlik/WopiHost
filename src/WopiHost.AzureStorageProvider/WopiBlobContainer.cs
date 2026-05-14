using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using WopiHost.Abstractions;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Virtual folder backed by a blob-name prefix. Plain Blob Storage has no folder objects, so a folder
/// is just a logical grouping of blobs that share a <c>/</c>-delimited prefix.
/// </summary>
/// <remarks>
/// The <see cref="Size"/> and <see cref="ChildCount"/> members enumerate the underlying blob
/// container on first access. Each property caches its result on the instance — repeated reads
/// hit the cache, so a single <see cref="WopiBlobContainer"/> is at most one round-trip per
/// member. Passing a <see langword="null"/> <paramref name="containerClient"/> keeps the
/// instance offline-friendly (used by unit tests that don't need the enumeration); the metadata
/// members return <c>0</c> in that mode.
/// </remarks>
public class WopiBlobContainer(string prefix, string identifier, BlobContainerClient? containerClient = null) : IWopiContainer
{
    private readonly BlobContainerClient? _containerClient = containerClient;
    private long? _size;
    private int? _childCount;

    /// <summary>Blob-name prefix this folder represents (no leading or trailing slash).</summary>
    public string Prefix { get; } = prefix;

    /// <inheritdoc/>
    public string Identifier { get; } = identifier;

    /// <inheritdoc/>
    public string Name => string.IsNullOrEmpty(Prefix)
        ? string.Empty
        : Prefix[(Prefix.LastIndexOf('/') + 1)..];

    /// <inheritdoc/>
    public long Size => _size ??= ComputeSize();

    /// <inheritdoc/>
    public int ChildCount => _childCount ??= ComputeChildCount();

    private long ComputeSize()
    {
        if (_containerClient is null)
        {
            return 0;
        }
        // Sum the content lengths of every blob under this prefix. Sync API on purpose — the
        // property contract on IWopiContainer is sync, and the caller has opted into the
        // round-trip cost by reading the property. Skip the folder-marker blobs (they're
        // bookkeeping artifacts, not real content).
        var listPrefix = string.IsNullOrEmpty(Prefix) ? null : Prefix + "/";
        long total = 0;
        foreach (var item in _containerClient.GetBlobs(traits: BlobTraits.None, states: BlobStates.None, prefix: listPrefix, cancellationToken: default))
        {
            if (IsFolderMarker(item.Name))
            {
                continue;
            }
            total += item.Properties.ContentLength ?? 0;
        }
        return total;
    }

    private int ComputeChildCount()
    {
        if (_containerClient is null)
        {
            return 0;
        }
        // Direct children only — GetBlobsByHierarchy with delimiter "/" returns one item per
        // top-level child (blob or virtual prefix). Skip the folder-marker blob the provider
        // drops to represent empty folders; it isn't a "real" child.
        var listPrefix = string.IsNullOrEmpty(Prefix) ? null : Prefix + "/";
        var count = 0;
        foreach (var item in _containerClient.GetBlobsByHierarchy(traits: BlobTraits.None, states: BlobStates.None, delimiter: "/", prefix: listPrefix, cancellationToken: default))
        {
            if (item.IsBlob && IsFolderMarker(item.Blob.Name))
            {
                continue;
            }
            count++;
        }
        return count;
    }

    private static bool IsFolderMarker(string blobName) =>
        blobName == BlobIdMap.FolderMarker
        || blobName.EndsWith("/" + BlobIdMap.FolderMarker, StringComparison.Ordinal);
}
