using System.Security.Cryptography;
using Azure.Storage.Blobs;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Wraps the blob upload stream returned by <see cref="BlobClient.OpenWriteAsync(bool, Azure.Storage.Blobs.Models.BlobOpenWriteOptions, System.Threading.CancellationToken)"/>
/// so callers can stream large content while we incrementally compute SHA-256 of the new payload.
/// On dispose, the hash is finalized and written back as the <see cref="WopiBlobFile.Sha256MetadataKey"/>
/// metadata key, preserving the metadata that existed before the write.
/// </summary>
internal sealed class HashingBlobWriteStream(Stream inner, BlobClient blobClient, Dictionary<string, string> preservedMetadata) : Stream
{
    private readonly Dictionary<string, string> _metadataToWrite = preservedMetadata;
    private readonly IncrementalHash _hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool _disposed;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        _hasher.AppendData(buffer, offset, count);
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _hasher.AppendData(buffer);
        inner.Write(buffer);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        _hasher.AppendData(buffer, offset, count);
        await inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _hasher.AppendData(buffer.Span);
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        // Closing/disposing the inner Azure write stream is what actually commits the blob.
        await inner.DisposeAsync().ConfigureAwait(false);
        FinalizeMetadataPayload();
        await blobClient.SetMetadataAsync(_metadataToWrite).ConfigureAwait(false);

        // Stream has a finalizer; skip it now that DisposeAsync has done the work. The sync
        // Stream.Dispose() base already calls SuppressFinalize, but the async path is on us.
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        // Synchronous dispose is unavoidable for callers that don't await DisposeAsync; do the
        // expensive bits inline. Intentionally no GetAwaiter().GetResult() on user-facing async
        // calls here — we already removed sync-over-async from the lock provider; this stream is the
        // last necessary sync-over-async boundary because Stream.Dispose can't be async.
        if (_disposed || !disposing)
        {
            return;
        }
        _disposed = true;

        inner.Dispose();
        FinalizeMetadataPayload();
        blobClient.SetMetadata(_metadataToWrite);
    }

    /// <summary>
    /// Finalizes the SHA-256 hasher and stores the lowercase-hex digest under the blob metadata
    /// key. Shared between <see cref="Dispose(bool)"/> and <see cref="DisposeAsync"/> so the
    /// two paths can never drift on the hex/casing/metadata-key contract (#409 item 2.12).
    /// </summary>
    private void FinalizeMetadataPayload()
    {
        var hash = _hasher.GetCurrentHash();
        _hasher.Dispose();
        _metadataToWrite[WopiBlobFile.Sha256MetadataKey] = Convert.ToHexString(hash).ToLowerInvariant();
    }
}
