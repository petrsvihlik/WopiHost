using System.Globalization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using WopiHost.Abstractions;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IWopiFile"/>.
/// </summary>
/// <param name="blobClient">Azure Blob client for the file</param>
/// <param name="fileIdentifier">Unique identifier of the file</param>
public class WopiAzureFile(BlobClient blobClient, string fileIdentifier) : IWopiFile
{
    private readonly BlobClient _blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));

    /// <inheritdoc/>
    public string Identifier { get; } = fileIdentifier ?? throw new ArgumentNullException(nameof(fileIdentifier));

    /// <inheritdoc/>
    public string Name => Path.GetFileNameWithoutExtension(blobClient.Name);

    /// <inheritdoc/>
    public bool Exists
    {
        get
        {
            try
            {
                return _blobClient.Exists().Value;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public string Extension => Path.GetExtension(_blobClient.Name).TrimStart('.');

    /// <inheritdoc/>
    public string? Version
    {
        get
        {
            try
            {
                var properties = _blobClient.GetProperties();
                return properties.Value.ETag.ToString();
            }
            catch
            {
                return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            }
        }
    }

    /// <inheritdoc/>
#pragma warning disable CA1819 // Properties should not return arrays
    public byte[]? Checksum
    {
        get
        {
            try
            {
                var properties = _blobClient.GetProperties();
                return properties.Value.ContentHash;
            }
            catch
            {
                return null;
            }
        }
    }
#pragma warning restore CA1819 // Properties should not return arrays

    /// <inheritdoc/>
    public long Size
    {
        get
        {
            try
            {
                var properties = _blobClient.GetProperties();
                return properties.Value.ContentLength;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <inheritdoc/>
    public long Length => Size;

    /// <inheritdoc/>
    public DateTime LastWriteTimeUtc
    {
        get
        {
            try
            {
                var properties = _blobClient.GetProperties();
                return properties.Value.LastModified.UtcDateTime;
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }
    }

    /// <inheritdoc/>
    public string Owner
    {
        get
        {
            try
            {
                var properties = _blobClient.GetProperties();
                return properties.Value.Metadata.TryGetValue("Owner", out var owner) ? owner : "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }

    /// <inheritdoc/>
    public async Task<Stream> GetReadStream(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException($"Blob '{_blobClient.Name}' not found.", ex);
        }
    }

    /// <inheritdoc/>
    public Task<Stream> GetWriteStream(CancellationToken cancellationToken = default)
    {
        // For write operations, we'll return a memory stream that will be uploaded when disposed
        return Task.FromResult<Stream>(new AzureBlobWriteStream(_blobClient, cancellationToken));
    }
}

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
