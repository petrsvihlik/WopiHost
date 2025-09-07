using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// A write stream that uploads to Azure Blob Storage when disposed.
/// </summary>
internal class AzureBlobWriteStream(BlobClient blobClient, CancellationToken cancellationToken) : Stream
{
    private readonly MemoryStream _memoryStream = new();
    private bool _disposed = false;

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
        await _memoryStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                _memoryStream.Position = 0;
                var uploadOptions = new BlobUploadOptions();
                blobClient.UploadAsync(_memoryStream, uploadOptions, cancellationToken).Wait();
            }
            catch
            {
                // Log error but don't throw during disposal
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
                _memoryStream.Position = 0;
                var uploadOptions = new BlobUploadOptions();
                await blobClient.UploadAsync(_memoryStream, uploadOptions, cancellationToken);
            }
            catch
            {
                // Log error but don't throw during disposal
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
