using System.Security.Cryptography;
using Azure.Storage.Blobs;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Wraps the blob upload stream returned by <see cref="BlobClient.OpenWriteAsync(bool, Azure.Storage.Blobs.Models.BlobOpenWriteOptions, System.Threading.CancellationToken)"/>
/// so callers can stream large content while we incrementally compute SHA-256 of the new payload.
/// On dispose, the hash is finalized and written back as the <see cref="WopiBlobFile.Sha256MetadataKey"/>
/// metadata key, preserving the metadata that existed before the write.
/// </summary>
internal sealed class HashingBlobWriteStream : Stream
{
    private readonly Stream inner;
    private readonly BlobClient blobClient;
    private readonly Dictionary<string, string> metadataToWrite;
    private readonly IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool disposed;

    public HashingBlobWriteStream(Stream inner, BlobClient blobClient, Dictionary<string, string> preservedMetadata)
    {
        this.inner = inner;
        this.blobClient = blobClient;
        this.metadataToWrite = preservedMetadata;
    }

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
        hasher.AppendData(buffer, offset, count);
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        hasher.AppendData(buffer);
        inner.Write(buffer);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        hasher.AppendData(buffer, offset, count);
        await inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        hasher.AppendData(buffer.Span);
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;

        // Closing/disposing the inner Azure write stream is what actually commits the blob.
        await inner.DisposeAsync().ConfigureAwait(false);

        var hash = hasher.GetCurrentHash();
        hasher.Dispose();
        metadataToWrite[WopiBlobFile.Sha256MetadataKey] = Convert.ToHexString(hash).ToLowerInvariant();

        await blobClient.SetMetadataAsync(metadataToWrite).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        // Synchronous dispose is unavoidable for callers that don't await DisposeAsync; do the
        // expensive bits inline. Intentionally no GetAwaiter().GetResult() on user-facing async
        // calls here — we already removed sync-over-async from the lock provider; this stream is the
        // last necessary sync-over-async boundary because Stream.Dispose can't be async.
        if (disposed || !disposing)
        {
            return;
        }
        disposed = true;

        inner.Dispose();
        var hash = hasher.GetCurrentHash();
        hasher.Dispose();
        metadataToWrite[WopiBlobFile.Sha256MetadataKey] = Convert.ToHexString(hash).ToLowerInvariant();
        blobClient.SetMetadata(metadataToWrite);
    }
}
