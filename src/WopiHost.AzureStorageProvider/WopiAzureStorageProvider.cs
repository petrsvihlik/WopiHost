using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// <see cref="IWopiStorageProvider"/> + <see cref="IWopiWritableStorageProvider"/> backed by Azure Blob Storage.
/// </summary>
/// <remarks>
/// <para>
/// Folders are virtual: they map to <c>/</c>-delimited blob-name prefixes. An "empty" folder is
/// materialized via a zero-byte folder-marker blob (<see cref="BlobIdMap.FolderMarker"/>) so that
/// freshly-created folders are addressable until something is written to them.
/// </para>
/// <para>
/// Identifiers are deterministic hex-SHA-256 of the lowercased blob path (see <see cref="BlobIdMap"/>),
/// matching the scheme used by <c>WopiHost.FileSystemProvider</c>. Identifiers are stable across
/// process restarts as long as paths don't change. A rename preserves the identifier by re-pointing
/// the in-memory map to the new path.
/// </para>
/// </remarks>
public partial class WopiAzureStorageProvider : IWopiStorageProvider, IWopiWritableStorageProvider
{
    private readonly BlobContainerClient _containerClient;
    private readonly BlobIdMap _idMap;
    private readonly ILogger<WopiAzureStorageProvider> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    /// <inheritdoc/>
    public IWopiContainer RootContainer { get; }

    /// <summary>Create the provider from a configured <see cref="BlobContainerClient"/>.</summary>
    public WopiAzureStorageProvider(
        BlobContainerClient containerClient,
        BlobIdMap idMap,
        ILogger<WopiAzureStorageProvider> logger)
    {
        _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        _idMap = idMap ?? throw new ArgumentNullException(nameof(idMap));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var rootId = BlobIdMap.IdFromPath(string.Empty);
        RootContainer = new WopiBlobContainer(string.Empty, rootId, _containerClient);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }
        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }
            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!_idMap.WasScanned)
            {
                var paths = new List<string>();
                await foreach (var item in _containerClient.GetBlobsAsync(traits: BlobTraits.None, states: BlobStates.None, prefix: null, cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    paths.Add(item.Name);
                }
                _idMap.ScanAll(paths);
            }
            _initialized = true;
            LogInitialized(_logger, _containerClient.Name);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IWopiFile?> GetWopiFile(string identifier, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(identifier, out var path))
        {
            return null;
        }
        var blobClient = _containerClient.GetBlobClient(path);
        return await WopiBlobFile.CreateAsync(blobClient, path, identifier, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IWopiWritableFile?> GetWritableFile(string identifier, CancellationToken cancellationToken = default)
    {
        // Same WopiBlobFile the read-side returns — the concrete class implements
        // IWopiWritableFile (extends IWopiFile), so read vs. writable is purely the static
        // type the caller sees.
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(identifier, out var path))
        {
            return null;
        }
        var blobClient = _containerClient.GetBlobClient(path);
        return await WopiBlobFile.CreateAsync(blobClient, path, identifier, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IWopiContainer?> GetWopiContainer(string identifier, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(identifier, out var path))
        {
            return null;
        }
        return new WopiBlobContainer(path, identifier, _containerClient);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFile> GetWopiFiles(
        string identifier,
        IReadOnlyCollection<string>? fileExtensions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var folderPath = ResolveFolderPath(identifier);
        var prefix = string.IsNullOrEmpty(folderPath) ? null : folderPath + "/";

        // Azure Blob Storage's list API exposes only a prefix filter at the wire level — no
        // suffix / extension / regex support — so the extension filter is applied at the
        // streaming-list boundary inside the loop. Each non-matching blob is dropped before
        // a WopiBlobFile is allocated for it. The hashset (OrdinalIgnoreCase) gives O(1)
        // membership checks regardless of how many extensions the caller asked for.
        var extensionFilter = (fileExtensions is { Count: > 0 })
            ? new HashSet<string>(fileExtensions, StringComparer.OrdinalIgnoreCase)
            : null;

        await foreach (var item in _containerClient.GetBlobsByHierarchyAsync(traits: BlobTraits.None, states: BlobStates.None, delimiter: "/", prefix: prefix, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (item.IsBlob is false)
            {
                continue;
            }
            var name = item.Blob.Name;
            // Skip the folder-marker blob — it represents the parent's existence, not a real file.
            if (name.EndsWith("/" + BlobIdMap.FolderMarker, StringComparison.Ordinal)
                || name == BlobIdMap.FolderMarker)
            {
                continue;
            }
            var fileName = name[(name.LastIndexOf('/') + 1)..];
            if (extensionFilter is not null && !extensionFilter.Contains(Path.GetExtension(fileName)))
            {
                continue;
            }
            var id = _idMap.TryGetFileId(name, out var existingId) ? existingId : _idMap.Add(name);
            var blobClient = _containerClient.GetBlobClient(name);
            yield return await WopiBlobFile.CreateAsync(blobClient, name, id, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiContainer> GetWopiContainers(
        string identifier,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var folderPath = ResolveFolderPath(identifier);
        var prefix = string.IsNullOrEmpty(folderPath) ? null : folderPath + "/";

        await foreach (var item in _containerClient.GetBlobsByHierarchyAsync(traits: BlobTraits.None, states: BlobStates.None, delimiter: "/", prefix: prefix, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (item.IsPrefix is false)
            {
                continue;
            }
            var subPrefix = item.Prefix.TrimEnd('/');
            var id = _idMap.TryGetFileId(subPrefix, out var existingId) ? existingId : _idMap.Add(subPrefix);
            yield return new WopiBlobContainer(subPrefix, id, _containerClient);
        }
    }

    /// <inheritdoc/>
    public async Task<ReadOnlyCollection<IWopiContainer>> GetFileAncestors(string fileId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(fileId, out var path))
        {
            throw new DirectoryNotFoundException($"Resource '{fileId}' not found.");
        }
        var result = new List<IWopiContainer>();
        // Always include the immediate parent for files (matches the WOPI EnumerateAncestors
        // example: /root/.../parent/myfile.docx → [root, ..., parent]).
        var parentPath = GetParentPath(path);
        var parentId = _idMap.TryGetFileId(parentPath, out var existingId) ? existingId : _idMap.Add(parentPath);
        result.Add(new WopiBlobContainer(parentPath, parentId, _containerClient));
        WalkAncestors(parentPath, result);
        result.Reverse();
        return result.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<ReadOnlyCollection<IWopiContainer>> GetContainerAncestors(string containerId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(containerId, out var path))
        {
            throw new DirectoryNotFoundException($"Resource '{containerId}' not found.");
        }
        var result = new List<IWopiContainer>();
        WalkAncestors(path, result);
        result.Reverse();
        return result.AsReadOnly();
    }

    private void WalkAncestors(string startPath, List<IWopiContainer> result)
    {
        var path = startPath;
        while (!string.IsNullOrEmpty(path))
        {
            path = GetParentPath(path);
            var ancestorId = _idMap.TryGetFileId(path, out var existingId) ? existingId : _idMap.Add(path);
            result.Add(new WopiBlobContainer(path, ancestorId, _containerClient));
            if (string.IsNullOrEmpty(path))
            {
                break;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<IWopiFile?> GetWopiFileByName(string containerId, string name, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(containerId, out var folderPath))
        {
            return null;
        }
        var combined = string.IsNullOrEmpty(folderPath) ? name : folderPath + "/" + name;
        var blobClient = _containerClient.GetBlobClient(combined);
        try
        {
            _ = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        var id = _idMap.TryGetFileId(combined, out var existingId) ? existingId : _idMap.Add(combined);
        return await WopiBlobFile.CreateAsync(blobClient, combined, id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IWopiContainer?> GetWopiContainerByName(string containerId, string name, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(containerId, out var folderPath))
        {
            return null;
        }
        var combined = string.IsNullOrEmpty(folderPath) ? name : folderPath + "/" + name;
        // Folder exists if any blob shares this prefix.
        await foreach (var _ in _containerClient.GetBlobsAsync(traits: BlobTraits.None, states: BlobStates.None, prefix: combined + "/", cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var id = _idMap.TryGetFileId(combined, out var existingId) ? existingId : _idMap.Add(combined);
            return new WopiBlobContainer(combined, id, _containerClient);
        }
        return null;
    }

    #region IWopiWritableStorageProvider

    /// <inheritdoc/>
    public int FileNameMaxLength => 250;

    /// <inheritdoc/>
    public Task<bool> CheckValidFileName(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(IsValidSingleSegmentName(name));

    /// <inheritdoc/>
    public Task<bool> CheckValidContainerName(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(IsValidSingleSegmentName(name));

    private bool IsValidSingleSegmentName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= FileNameMaxLength
        // Single segment — no slashes, no path nav.
        && !name.Contains('/') && !name.Contains('\\') && name != "." && name != ".."
        // Folder-marker is reserved.
        && name != BlobIdMap.FolderMarker
        // Azure blob naming spec disallows control characters.
        && !name.Any(char.IsControl);

    /// <inheritdoc/>
    public Task<string> GetSuggestedFileName(string containerId, string name, CancellationToken cancellationToken = default)
        => GetSuggestedNameAsync(containerId, name, isFile: true, cancellationToken);

    /// <inheritdoc/>
    public Task<string> GetSuggestedContainerName(string containerId, string name, CancellationToken cancellationToken = default)
        => GetSuggestedNameAsync(containerId, name, isFile: false, cancellationToken);

    private async Task<string> GetSuggestedNameAsync(string containerId, string name, bool isFile, CancellationToken cancellationToken)
    {
        var valid = isFile
            ? await CheckValidFileName(name, cancellationToken).ConfigureAwait(false)
            : await CheckValidContainerName(name, cancellationToken).ConfigureAwait(false);
        if (!valid)
        {
            throw new ArgumentException("Invalid characters in the name.", nameof(name));
        }
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(containerId, out _))
        {
            throw new DirectoryNotFoundException($"Container '{containerId}' not found.");
        }

        var candidate = name;
        var counter = 1;
        while (await ExistsByNameAsync(containerId, candidate, isFile, cancellationToken).ConfigureAwait(false))
        {
            candidate = isFile
                ? FormatSuggestedFileName(name, counter++)
                : $"{name} ({counter++.ToString(CultureInfo.InvariantCulture)})";
        }
        return candidate;
    }

    private async Task<bool> ExistsByNameAsync(string containerId, string name, bool isFile, CancellationToken cancellationToken)
    {
        if (isFile)
        {
            return await GetWopiFileByName(containerId, name, cancellationToken).ConfigureAwait(false) is not null;
        }
        return await GetWopiContainerByName(containerId, name, cancellationToken).ConfigureAwait(false) is not null;
    }

    /// <inheritdoc/>
    public async Task<IWopiWritableFile?> CreateWopiChildFile(string containerId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(containerId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(containerId, out var parentPath))
        {
            throw new DirectoryNotFoundException($"Container '{containerId}' not found.");
        }

        var path = string.IsNullOrEmpty(parentPath) ? name : parentPath + "/" + name;
        var blobClient = _containerClient.GetBlobClient(path);
        try
        {
            using var empty = new MemoryStream([], writable: false);
            await blobClient.UploadAsync(empty, overwrite: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new ArgumentException($"File '{path}' already exists.", nameof(name));
        }
        var id = _idMap.Add(path);
        LogFileCreated(_logger, id, path);
        return await WopiBlobFile.CreateAsync(blobClient, path, id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IWopiContainer?> CreateWopiChildContainer(string containerId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(containerId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(containerId, out var parentPath))
        {
            throw new DirectoryNotFoundException($"Container '{containerId}' not found.");
        }

        var path = string.IsNullOrEmpty(parentPath) ? name : parentPath + "/" + name;
        var markerPath = path + "/" + BlobIdMap.FolderMarker;
        var markerClient = _containerClient.GetBlobClient(markerPath);
        try
        {
            using var empty = new MemoryStream([], writable: false);
            await markerClient.UploadAsync(empty, overwrite: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new ArgumentException($"Folder '{path}' already exists.", nameof(name));
        }
        var id = _idMap.Add(path);
        LogFolderCreated(_logger, id, path);
        return new WopiBlobContainer(path, id, _containerClient);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteWopiFile(string identifier, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(identifier, out var path))
        {
            return false;
        }
        var blobClient = _containerClient.GetBlobClient(path);
        var deleted = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (deleted.Value)
        {
            _idMap.Remove(identifier);
            LogFileDeleted(_logger, identifier, path);
        }
        return deleted.Value;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteWopiContainer(string identifier, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(identifier, out var path))
        {
            return false;
        }
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException("Cannot delete the root container.");
        }
        var prefix = path + "/";

        // Check the folder is empty (other than possibly the marker blob).
        await foreach (var item in _containerClient.GetBlobsAsync(traits: BlobTraits.None, states: BlobStates.None, prefix: prefix, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (item.Name != prefix + BlobIdMap.FolderMarker)
            {
                throw new InvalidOperationException($"Folder '{path}' is not empty.");
            }
        }

        // Delete the marker if present, then drop the id.
        var markerClient = _containerClient.GetBlobClient(prefix + BlobIdMap.FolderMarker);
        await markerClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        _idMap.Remove(identifier);
        LogFolderDeleted(_logger, identifier, path);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RenameWopiFile(string identifier, string requestedName, CancellationToken cancellationToken = default)
    {
        if (!await CheckValidFileName(requestedName, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException("Invalid characters in the name.", nameof(requestedName));
        }
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(identifier, out var oldPath))
        {
            return false;
        }
        var parent = GetParentPath(oldPath);
        var newPath = string.IsNullOrEmpty(parent) ? requestedName : parent + "/" + requestedName;
        await CopyAndDeleteBlobAsync(oldPath, newPath, cancellationToken).ConfigureAwait(false);
        _idMap.Update(identifier, newPath);
        LogFileRenamed(_logger, identifier, oldPath, newPath);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RenameWopiContainer(string identifier, string requestedName, CancellationToken cancellationToken = default)
    {
        if (!await CheckValidContainerName(requestedName, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException("Invalid characters in the name.", nameof(requestedName));
        }
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!_idMap.TryGetPath(identifier, out var oldPath))
        {
            return false;
        }
        if (string.IsNullOrEmpty(oldPath))
        {
            throw new InvalidOperationException("Cannot rename the root container.");
        }
        var parent = GetParentPath(oldPath);
        var oldPrefix = oldPath + "/";
        var newPath = string.IsNullOrEmpty(parent) ? requestedName : parent + "/" + requestedName;
        var newPrefix = newPath + "/";

        // Plain blob storage has no atomic rename — copy each blob then delete the original.
        // Order matters for crash safety: copy first, only delete after the copy succeeded.
        var renamed = new List<(string oldName, string newName, string? oldId)>();
        await foreach (var item in _containerClient.GetBlobsAsync(traits: BlobTraits.None, states: BlobStates.None, prefix: oldPrefix, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var rel = item.Name[oldPrefix.Length..];
            var dest = newPrefix + rel;
            await CopyAndDeleteBlobAsync(item.Name, dest, cancellationToken).ConfigureAwait(false);
            _idMap.TryGetFileId(item.Name, out var blobId);
            renamed.Add((item.Name, dest, blobId));
        }

        // Update the id map: keep the folder identifier stable, re-point children to new paths.
        _idMap.Update(identifier, newPath);
        foreach (var (_, newName, oldId) in renamed)
        {
            if (oldId is not null)
            {
                _idMap.Update(oldId, newName);
            }
        }
        LogFolderRenamed(_logger, identifier, oldPath, newPath, renamed.Count);
        return true;
    }

    #endregion

    private string ResolveFolderPath(string identifier)
    {
        if (_idMap.TryGetPath(identifier, out var path))
        {
            return path;
        }
        throw new DirectoryNotFoundException($"Container '{identifier}' not found.");
    }

    private static string GetParentPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }
        var slash = path.LastIndexOf('/');
        return slash < 0 ? string.Empty : path[..slash];
    }

    private async Task CopyAndDeleteBlobAsync(string sourcePath, string destPath, CancellationToken cancellationToken)
    {
        var source = _containerClient.GetBlobClient(sourcePath);
        var dest = _containerClient.GetBlobClient(destPath);

        // Atomic existence check via conditional-headers (IfNoneMatch="*") rather than a
        // separate ExistsAsync + StartCopyFromUri pair, which would have a TOCTOU window where
        // a concurrent rename could overwrite the destination. IfNoneMatch=* tells Azure to
        // "fail if the destination already has any ETag" — the 409 Conflict comes from Blob
        // Storage's own consistency layer, so no client-side race is possible.
        // https://learn.microsoft.com/rest/api/storageservices/specifying-conditional-headers-for-blob-service-operations
        try
        {
            var copyOp = await dest.StartCopyFromUriAsync(
                source.Uri,
                options: new BlobCopyFromUriOptions
                {
                    DestinationConditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await copyOp.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 409 || ex.ErrorCode == BlobErrorCode.BlobAlreadyExists)
        {
            // Callers (RenameWopi{File,Container}) map this to a 409 Conflict. Status 409 covers
            // the IfNoneMatch=* rejection and Azure's own BlobAlreadyExists path; both indicate
            // "destination occupied."
            throw new InvalidOperationException($"Target blob '{destPath}' already exists.", ex);
        }

        await source.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string FormatSuggestedFileName(string name, int counter)
    {
        var dot = name.LastIndexOf('.');
        if (dot <= 0)
        {
            return $"{name} ({counter.ToString(CultureInfo.InvariantCulture)})";
        }
        var stem = name[..dot];
        var ext = name[dot..];
        return $"{stem} ({counter.ToString(CultureInfo.InvariantCulture)}){ext}";
    }
}
