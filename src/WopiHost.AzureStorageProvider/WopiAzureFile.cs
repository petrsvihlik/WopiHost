using System.Globalization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IWopiFile"/>.
/// </summary>
/// <param name="blobClient">Azure Blob client for the file</param>
/// <param name="fileIdentifier">Unique identifier of the file</param>
/// <param name="logger">Optional logger for diagnostics</param>
public class WopiAzureFile(BlobClient blobClient, string fileIdentifier, ILogger<WopiAzureFile>? logger = null) : IWopiFile
{
    private readonly BlobClient _blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
    private readonly ILogger<WopiAzureFile>? _logger = logger;
    private BlobProperties? _cachedProperties;
    private DateTimeOffset _cacheTime;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public string Identifier { get; } = fileIdentifier ?? throw new ArgumentNullException(nameof(fileIdentifier));

    /// <summary>
    /// Gets cached blob properties or fetches them if not cached or expired.
    /// </summary>
    private BlobProperties? GetCachedProperties()
    {
        if (_cachedProperties == null || DateTimeOffset.UtcNow - _cacheTime > _cacheLifetime)
        {
            try
            {
                _cachedProperties = _blobClient.GetProperties().Value;
                _cacheTime = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning(ex, "Failed to get blob properties for {BlobName}", _blobClient.Name);
                }
                _cachedProperties = null;
            }
        }
        return _cachedProperties;
    }

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
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning(ex, "Failed to check if blob exists: {BlobName}", _blobClient.Name);
                }
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
            var properties = GetCachedProperties();
            if (properties != null)
            {
                return properties.ETag.ToString();
            }
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }
    }

    /// <inheritdoc/>
#pragma warning disable CA1819 // Properties should not return arrays
    public byte[]? Checksum
    {
        get
        {
            var properties = GetCachedProperties();
            return properties?.ContentHash;
        }
    }
#pragma warning restore CA1819 // Properties should not return arrays

    /// <inheritdoc/>
    public long Size
    {
        get
        {
            var properties = GetCachedProperties();
            return properties?.ContentLength ?? 0;
        }
    }

    /// <inheritdoc/>
    public long Length => Size;

    /// <inheritdoc/>
    public DateTime LastWriteTimeUtc
    {
        get
        {
            var properties = GetCachedProperties();
            return properties?.LastModified.UtcDateTime ?? DateTime.UtcNow;
        }
    }

    /// <inheritdoc/>
    public string Owner
    {
        get
        {
            var properties = GetCachedProperties();
            if (properties?.Metadata.TryGetValue("Owner", out var owner) == true)
            {
                return owner;
            }
            return "Unknown";
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

