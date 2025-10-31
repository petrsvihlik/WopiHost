using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// A write stream that uploads to Azure Blob Storage when disposed.
/// IMPORTANT: This stream MUST be disposed using DisposeAsync() to ensure data is uploaded.
/// Synchronous Dispose() will throw an exception if data hasn't been uploaded.
/// </summary>
internal class AzureBlobWriteStream(BlobClient blobClient, CancellationToken cancellationToken) : Stream
{
    private readonly MemoryStream _memoryStream = new();
    private bool _disposed = false;
    private bool _uploaded = false;

    public override bool CanRead => _memoryStream.CanRead;
    public override bool CanSeek => _memoryStream.CanSeek;
    public override bool CanWrite => _memoryStream.CanWrite;
    public override long Length => _memoryStream.Length;
    public override long Position
    {
        get => _memoryStream.Position;
        set => _memoryStream.Position = value;
    }

    public override void Flush() => _memoryStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) => _memoryStream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => _memoryStream.Seek(offset, origin);

    public override void SetLength(long value) => _memoryStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => _memoryStream.Write(buffer, offset, count);

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _memoryStream.WriteAsync(buffer, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                // Synchronous dispose should not perform async I/O operations
                // Throw if data hasn't been uploaded via DisposeAsync
                if (_memoryStream.Length > 0 && !_uploaded)
                {
                    throw new InvalidOperationException(
                        "AzureBlobWriteStream contains unuploaded data. " +
                        "You must use DisposeAsync() or await using statement to ensure data is uploaded to Azure Blob Storage.");
                }
            }
            finally
            {
                _memoryStream.Dispose();
                _disposed = true;
            }
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            try
            {
                // Only upload if there's data and it hasn't been uploaded yet
                if (_memoryStream.Length > 0 && !_uploaded)
                {
                    _memoryStream.Position = 0;
                    var uploadOptions = new BlobUploadOptions();
                    
                    try
                    {
                        await blobClient.UploadAsync(_memoryStream, uploadOptions, cancellationToken);
                        _uploaded = true;
                    }
                    catch (OperationCanceledException)
                    {
                        // If operation was cancelled, try with a new token that won't be immediately cancelled
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        await blobClient.UploadAsync(_memoryStream, uploadOptions, cts.Token);
                        _uploaded = true;
                    }
                }
            }
            finally
            {
                await _memoryStream.DisposeAsync();
                _disposed = true;
            }
        }
        await base.DisposeAsync();
    }
}
