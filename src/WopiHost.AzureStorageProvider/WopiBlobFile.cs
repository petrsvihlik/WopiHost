using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using WopiHost.Abstractions;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// <see cref="IWopiFile"/> backed by an Azure Blob.
/// </summary>
/// <remarks>
/// <para>
/// Properties (<see cref="Length"/>, <see cref="LastWriteTimeUtc"/>, <see cref="Version"/>, <see cref="Owner"/>,
/// <see cref="Checksum"/>) are eagerly fetched once via <see cref="CreateAsync"/> and cached on the instance,
/// so subsequent property reads are local. The <see cref="IWopiStorageProvider"/> creates a fresh
/// <see cref="WopiBlobFile"/> per request, which keeps cached state implicitly fresh.
/// </para>
/// <para>
/// Identifiers are hex-MD5 of the lowercased blob path (see <see cref="BlobIdMap"/>). The blob's ETag is
/// surfaced as <see cref="Version"/>; this is more meaningful than a timestamp because every byte-level
/// change produces a new ETag.
/// </para>
/// </remarks>
public class WopiBlobFile : IWopiFile
{
    /// <summary>Blob metadata key that holds the file's owner identifier (free-form).</summary>
    public const string OwnerMetadataKey = "wopi_owner";

    /// <summary>Blob metadata key that holds the SHA-256 of the blob content as a lowercase hex string.</summary>
    public const string Sha256MetadataKey = "wopi_sha256";

    private readonly BlobClient blobClient;
    private readonly BlobProperties? properties;

    private WopiBlobFile(BlobClient blobClient, string blobPath, string identifier, BlobProperties? properties)
    {
        this.blobClient = blobClient;
        this.properties = properties;
        BlobPath = blobPath;
        Identifier = identifier;
    }

    /// <summary>Full blob path within the container (no leading slash).</summary>
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
    public bool Exists => properties is not null;

    /// <inheritdoc/>
    public long Length => properties?.ContentLength ?? 0;

    /// <inheritdoc/>
    public long Size => Length;

    /// <inheritdoc/>
    public DateTime LastWriteTimeUtc => properties?.LastModified.UtcDateTime ?? DateTime.MinValue;

    /// <inheritdoc/>
    public string? Version => properties?.ETag.ToString();

    /// <inheritdoc/>
    public string Owner
    {
        get
        {
            if (properties is { Metadata: { } meta } && meta.TryGetValue(OwnerMetadataKey, out var owner))
            {
                return owner;
            }
            return string.Empty;
        }
    }

    /// <inheritdoc/>
#pragma warning disable CA1819 // Properties should not return arrays — interface contract
    public byte[]? Checksum
#pragma warning restore CA1819
    {
        get
        {
            if (properties is { Metadata: { } meta } && meta.TryGetValue(Sha256MetadataKey, out var hex) && !string.IsNullOrEmpty(hex))
            {
                return Convert.FromHexString(hex);
            }
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<Stream> GetReadStream(CancellationToken cancellationToken = default)
        => await blobClient.OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    /// <remarks>
    /// Returns a stream that wraps the blob's upload stream and incrementally computes the SHA-256
    /// of the new content. On dispose the hash is finalized and written back as the
    /// <see cref="Sha256MetadataKey"/> metadata key, preserving any pre-existing metadata (owner,
    /// custom keys) so they survive the rewrite.
    /// </remarks>
    public async Task<Stream> GetWriteStream(CancellationToken cancellationToken = default)
    {
        var preserved = properties?.Metadata is { } existing
            ? new Dictionary<string, string>(existing, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        var inner = await blobClient.OpenWriteAsync(overwrite: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new HashingBlobWriteStream(inner, blobClient, preserved);
    }

    /// <summary>Fetches blob properties (or returns null on 404) and produces a fully-populated <see cref="WopiBlobFile"/>.</summary>
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
