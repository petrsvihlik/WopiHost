using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using WopiHost.Abstractions;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Represents an Azure blob as an <see cref="IWopiWritableFile"/>.
/// </summary>
/// <remarks>
/// <para>
/// Properties (<see cref="Length"/>, <see cref="LastWriteTimeUtc"/>, <see cref="Version"/>, <see cref="Owner"/>,
/// <see cref="Checksum"/>) are eagerly fetched once via <see cref="CreateAsync"/> and cached on the instance,
/// so subsequent property reads are local. <see cref="WopiAzureStorageProvider"/> creates a fresh
/// <see cref="WopiBlobFile"/> per request, which keeps cached state implicitly fresh.
/// </para>
/// <para>
/// Identifiers are hex-SHA-256 of the blob path hashed as-is, case-sensitively (see <see cref="BlobIdMap"/>). The blob's ETag is
/// surfaced as <see cref="Version"/>, since every byte-level change produces a new ETag.
/// </para>
/// </remarks>
public class WopiBlobFile : IWopiWritableFile
{
    /// <summary>The blob metadata key that holds the file's owner identifier (free-form).</summary>
    public const string OwnerMetadataKey = "wopi_owner";

    /// <summary>The blob metadata key that holds the SHA-256 of the blob content as a lowercase hex string.</summary>
    public const string Sha256MetadataKey = "wopi_sha256";

    private readonly BlobClient _blobClient;
    private readonly BlobProperties? _properties;

    private WopiBlobFile(BlobClient blobClient, string blobPath, string identifier, BlobProperties? properties)
    {
        _blobClient = blobClient;
        _properties = properties;
        BlobPath = blobPath;
        Identifier = identifier;
    }

    /// <summary>Gets the full blob path within the container (no leading slash).</summary>
    public string BlobPath { get; }

    /// <inheritdoc/>
    public string Identifier { get; }

    /// <inheritdoc/>
    public string Name
    {
        get
        {
            var fileName = BlobPath[(BlobPath.LastIndexOf('/') + 1)..];
            var dot = fileName.LastIndexOf('.');
            return dot < 0 ? fileName : fileName[..dot];
        }
    }

    /// <inheritdoc/>
    public string Extension
    {
        get
        {
            var fileName = BlobPath[(BlobPath.LastIndexOf('/') + 1)..];
            var dot = fileName.LastIndexOf('.');
            return dot < 0 ? string.Empty : fileName[(dot + 1)..];
        }
    }

    /// <inheritdoc/>
    public bool Exists => _properties is not null;

    /// <inheritdoc/>
    public long Length => _properties?.ContentLength ?? 0;

    /// <inheritdoc/>
    public DateTime LastWriteTimeUtc => _properties?.LastModified.UtcDateTime ?? DateTime.MinValue;

    /// <inheritdoc/>
    /// <remarks>The blob's ETag, or <see langword="null"/> when the blob does not exist.</remarks>
    public string? Version => _properties?.ETag.ToString();

    /// <inheritdoc/>
    /// <remarks>Read from the <see cref="OwnerMetadataKey"/> blob metadata entry.</remarks>
    public string Owner
    {
        get
        {
            if (_properties is { Metadata: { } meta } && meta.TryGetValue(OwnerMetadataKey, out var owner))
            {
                return owner;
            }
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Read from the <see cref="Sha256MetadataKey"/> blob metadata entry, which
    /// <see cref="OpenWriteAsync"/> maintains; <see langword="null"/> for a blob written by other means.
    /// </remarks>
    public ReadOnlyMemory<byte>? Checksum
    {
        get
        {
            if (_properties is { Metadata: { } meta } && meta.TryGetValue(Sha256MetadataKey, out var hex) && !string.IsNullOrEmpty(hex))
            {
                return Convert.FromHexString(hex);
            }
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        => await _blobClient.OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    /// <remarks>
    /// Returns a stream that wraps the blob's upload stream and incrementally computes the SHA-256
    /// of the new content. On dispose the hash is finalized and written back as the
    /// <see cref="Sha256MetadataKey"/> metadata key, together with the metadata (owner, custom keys)
    /// captured when this file was loaded; metadata changed on the blob after that is overwritten.
    /// </remarks>
    public async Task<Stream> OpenWriteAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, string> preserved = _properties?.Metadata is { } existing
            ? new(existing, StringComparer.Ordinal)
            : [with(StringComparer.Ordinal)];

        var inner = await _blobClient.OpenWriteAsync(overwrite: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new HashingBlobWriteStream(inner, _blobClient, preserved);
    }

    /// <summary>Creates a <see cref="WopiBlobFile"/> with the blob's properties fetched up front.</summary>
    /// <param name="blobClient">The client for the blob.</param>
    /// <param name="blobPath">The full blob path within the container.</param>
    /// <param name="identifier">The unique identifier of the file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The file. When the blob does not exist, <see cref="Exists"/> is <see langword="false"/> and the
    /// other properties hold defaults.
    /// </returns>
    public static async Task<WopiBlobFile> CreateAsync(BlobClient blobClient, string blobPath, string identifier, CancellationToken cancellationToken)
    {
        BlobProperties? props = null;
        try
        {
            props = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Blob does not exist — Exists will be false, all other properties default.
        }
        return new WopiBlobFile(blobClient, blobPath, identifier, props);
    }
}
