using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Provides unique file identifiers for Azure Blob Storage files.
/// </summary>
public class AzureFileIds(ILogger<AzureFileIds> logger)
{
    private readonly Dictionary<string, string> fileIds = [];
    private readonly Dictionary<string, string> pathToId = [];

    /// <summary>
    /// Gets a value indicating whether any files have been scanned.
    /// </summary>
    public bool WasScanned => fileIds.Count > 0;

    /// <summary>
    /// Gets the file identifier for the specified blob path.
    /// </summary>
    /// <param name="blobPath">The blob path</param>
    /// <param name="fileId">The file identifier if found</param>
    /// <returns>True if the file ID was found</returns>
    public bool TryGetFileId(string blobPath, [NotNullWhen(true)] out string? fileId)
    {
        fileId = pathToId.GetValueOrDefault(blobPath);
        return fileId != null;
    }

    /// <summary>
    /// Gets the blob path for the specified file identifier.
    /// </summary>
    /// <param name="fileId">The file identifier</param>
    /// <param name="blobPath">The blob path if found</param>
    /// <returns>True if the blob path was found</returns>
    public bool TryGetPath(string fileId, [NotNullWhen(true)] out string? blobPath)
    {
        return fileIds.TryGetValue(fileId, out blobPath);
    }

    /// <summary>
    /// Gets the blob path for the specified file identifier.
    /// </summary>
    /// <param name="fileId">The file identifier</param>
    /// <returns>The blob path or null if not found</returns>
    public string? GetPath(string fileId)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileId);
        if (TryGetPath(fileId, out var path))
        {
            return path;
        }
        return null;
    }

    /// <summary>
    /// Adds a file to the collection and returns its identifier.
    /// </summary>
    /// <param name="blobPath">The blob path</param>
    /// <returns>The generated file identifier</returns>
    public string AddFile(string blobPath)
    {
        var id = NewId();
        fileIds[id] = blobPath;
        pathToId[blobPath] = id;
        return id;
    }

    /// <summary>
    /// Removes the specified file identifier.
    /// </summary>
    /// <param name="fileId">The file identifier to remove</param>
    public void RemoveId(string fileId)
    {
        if (fileIds.TryGetValue(fileId, out var blobPath))
        {
            fileIds.Remove(fileId);
            pathToId.Remove(blobPath);
        }
    }

    /// <summary>
    /// Updates the blob path for the specified file identifier.
    /// </summary>
    /// <param name="id">The file identifier</param>
    /// <param name="newBlobPath">The new blob path</param>
    public void UpdateFile(string id, string newBlobPath)
    {
        if (fileIds.TryGetValue(id, out var oldBlobPath))
        {
            pathToId.Remove(oldBlobPath);
        }
        fileIds[id] = newBlobPath;
        pathToId[newBlobPath] = id;
    }

    /// <summary>
    /// Scans all blobs in the specified container and root path.
    /// </summary>
    /// <param name="containerClient">The Azure Blob Container client</param>
    /// <param name="rootPath">The root path within the container</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ScanAllAsync(Azure.Storage.Blobs.BlobContainerClient containerClient, string? rootPath = null, CancellationToken cancellationToken = default)
    {
        fileIds.Clear();
        pathToId.Clear();

        // Add root container
        var rootId = NewId();
        var rootBlobPath = rootPath ?? string.Empty;
        fileIds[rootId] = rootBlobPath;
        pathToId[rootBlobPath] = rootId;

        try
        {
            var prefix = rootPath?.TrimEnd('/') + "/";
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                var blobPath = blobItem.Name;
                var newId = blobPath.EndsWith("test.wopitest", StringComparison.OrdinalIgnoreCase)
                    ? "WOPITEST"
                    : NewId();
                fileIds[newId] = blobPath;
                pathToId[blobPath] = newId;
            }

            logger.LogInformation("Scanned {total} items from Azure Blob Storage", fileIds.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scanning Azure Blob Storage container");
            throw;
        }
    }

    /// <summary>
    /// Creates a unique identifier.
    /// </summary>
    /// <returns>A unique identifier</returns>
    private static string NewId() => Guid.NewGuid().ToString("N");
}
