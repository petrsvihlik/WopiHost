using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
/// Identifiers are deterministic hex-MD5 of the lowercased blob path (see <see cref="BlobIdMap"/>),
/// matching the scheme used by <c>WopiHost.FileSystemProvider</c>. Identifiers are stable across
/// process restarts as long as paths don't change. A rename preserves the identifier by re-pointing
/// the in-memory map to the new path.
/// </para>
/// </remarks>
public class WopiAzureStorageProvider : IWopiStorageProvider, IWopiWritableStorageProvider
{
    private readonly BlobContainerClient containerClient;
    private readonly BlobIdMap idMap;
    private readonly ILogger<WopiAzureStorageProvider> logger;
    private readonly SemaphoreSlim initLock = new(1, 1);
    private bool initialized;

    /// <inheritdoc/>
    public IWopiFolder RootContainerPointer { get; }

    /// <summary>Create the provider from a configured <see cref="BlobContainerClient"/>.</summary>
    public WopiAzureStorageProvider(
        BlobContainerClient containerClient,
        BlobIdMap idMap,
        ILogger<WopiAzureStorageProvider> logger)
    {
        this.containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        this.idMap = idMap ?? throw new ArgumentNullException(nameof(idMap));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var rootId = BlobIdMap.IdFromPath(string.Empty);
        RootContainerPointer = new WopiBlobFolder(string.Empty, rootId);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (initialized)
        {
            return;
        }
        await initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (initialized)
            {
                return;
            }
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!idMap.WasScanned)
            {
                var paths = new List<string>();
                await foreach (var item in containerClient.GetBlobsAsync(traits: BlobTraits.None, states: BlobStates.None, prefix: null, cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    paths.Add(item.Name);
                }
                idMap.ScanAll(paths);
            }
            initialized = true;
            logger.LogInformation("WopiAzureStorageProvider initialized for container {Container}", containerClient.Name);
        }
        finally
        {
            initLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!idMap.TryGetPath(identifier, out var path))
        {
            return null;
        }

        if (typeof(IWopiFile).IsAssignableFrom(typeof(T)))
        {
            var blobClient = containerClient.GetBlobClient(path);
            return await WopiBlobFile.CreateAsync(blobClient, path, identifier, cancellationToken).ConfigureAwait(false) as T;
        }
        if (typeof(IWopiFolder).IsAssignableFrom(typeof(T)))
        {
            return new WopiBlobFolder(path, identifier) as T;
        }
        throw new NotSupportedException($"Unsupported resource type: {typeof(T).Name}");
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFile> GetWopiFiles(
        string? identifier = null,
        string? searchPattern = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var folderPath = ResolveFolderPath(identifier ?? RootContainerPointer.Identifier);
        var prefix = string.IsNullOrEmpty(folderPath) ? null : folderPath + "/";

        var matcher = BuildSearchMatcher(searchPattern);

        await foreach (var item in containerClient.GetBlobsByHierarchyAsync(traits: BlobTraits.None, states: BlobStates.None, delimiter: "/", prefix: prefix, cancellationToken: cancellationToken).ConfigureAwait(false))
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
            if (matcher is not null && !matcher(fileName))
            {
                continue;
            }
            var id = idMap.TryGetFileId(name, out var existingId) ? existingId : idMap.Add(name);
            var blobClient = containerClient.GetBlobClient(name);
            yield return await WopiBlobFile.CreateAsync(blobClient, name, id, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFolder> GetWopiContainers(
        string? identifier = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var folderPath = ResolveFolderPath(identifier ?? RootContainerPointer.Identifier);
        var prefix = string.IsNullOrEmpty(folderPath) ? null : folderPath + "/";

        await foreach (var item in containerClient.GetBlobsByHierarchyAsync(traits: BlobTraits.None, states: BlobStates.None, delimiter: "/", prefix: prefix, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (item.IsPrefix is false)
            {
                continue;
            }
            var subPrefix = item.Prefix.TrimEnd('/');
            var id = idMap.TryGetFileId(subPrefix, out var existingId) ? existingId : idMap.Add(subPrefix);
            yield return new WopiBlobFolder(subPrefix, id);
        }
    }

    /// <inheritdoc/>
    public async Task<ReadOnlyCollection<IWopiFolder>> GetAncestors<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!idMap.TryGetPath(identifier, out var path))
        {
            throw new DirectoryNotFoundException($"Resource '{identifier}' not found.");
        }

        var result = new List<IWopiFolder>();

        if (typeof(IWopiFile).IsAssignableFrom(typeof(T)))
        {
            // Always include the immediate parent for files (matches WopiFileSystemProvider behavior).
            var parentPath = GetParentPath(path);
            var parentId = idMap.TryGetFileId(parentPath, out var existingId) ? existingId : idMap.Add(parentPath);
            result.Add(new WopiBlobFolder(parentPath, parentId));
            path = parentPath;
        }

        while (!string.IsNullOrEmpty(path))
        {
            path = GetParentPath(path);
            var ancestorId = idMap.TryGetFileId(path, out var existingId) ? existingId : idMap.Add(path);
            result.Add(new WopiBlobFolder(path, ancestorId));
            if (string.IsNullOrEmpty(path))
            {
                break;
            }
        }
        result.Reverse();
        return result.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<T?> GetWopiResourceByName<T>(string containerId, string name, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!idMap.TryGetPath(containerId, out var folderPath))
        {
            throw new DirectoryNotFoundException($"Container '{containerId}' not found.");
        }
        var combined = string.IsNullOrEmpty(folderPath) ? name : folderPath + "/" + name;

        if (typeof(IWopiFile).IsAssignableFrom(typeof(T)))
        {
            var blobClient = containerClient.GetBlobClient(combined);
            try
            {
                _ = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
            var id = idMap.TryGetFileId(combined, out var existingId) ? existingId : idMap.Add(combined);
            return await WopiBlobFile.CreateAsync(blobClient, combined, id, cancellationToken).ConfigureAwait(false) as T;
        }
        if (typeof(IWopiFolder).IsAssignableFrom(typeof(T)))
        {
            // Folder exists if any blob shares this prefix.
            await foreach (var _ in containerClient.GetBlobsAsync(traits: BlobTraits.None, states: BlobStates.None, prefix: combined + "/", cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                var id = idMap.TryGetFileId(combined, out var existingId) ? existingId : idMap.Add(combined);
                return new WopiBlobFolder(combined, id) as T;
            }
            return null;
        }
        throw new NotSupportedException($"Unsupported resource type: {typeof(T).Name}");
    }

    #region IWopiWritableStorageProvider

    /// <inheritdoc/>
    public int FileNameMaxLength => 250;

    /// <inheritdoc/>
    public Task<bool> CheckValidName<T>(string name, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(false);
        }
        if (name.Length > FileNameMaxLength)
        {
            return Task.FromResult(false);
        }
        // Single segment — no slashes, no path nav.
        if (name.Contains('/') || name.Contains('\\') || name == "." || name == "..")
        {
            return Task.FromResult(false);
        }
        // Azure blob naming spec disallows control characters.
        foreach (var ch in name)
        {
            if (char.IsControl(ch))
            {
                return Task.FromResult(false);
            }
        }
        // Folder-marker is reserved.
        if (name == BlobIdMap.FolderMarker)
        {
            return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task<string> GetSuggestedName<T>(string containerId, string name, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        if (!await CheckValidName<T>(name, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException("Invalid characters in the name.", nameof(name));
        }
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!idMap.TryGetPath(containerId, out var folderPath))
        {
            throw new DirectoryNotFoundException($"Container '{containerId}' not found.");
        }

        var existing = await GetWopiResourceByName<T>(containerId, name, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return name;
        }

        var candidate = name;
        var counter = 1;
        while (await GetWopiResourceByName<T>(containerId, candidate, cancellationToken).ConfigureAwait(false) is not null)
        {
            candidate = typeof(IWopiFile).IsAssignableFrom(typeof(T))
                ? FormatSuggestedFileName(name, counter++)
                : $"{name} ({counter++})";
        }
        return candidate;
    }

    /// <inheritdoc/>
    public async Task<T?> CreateWopiChildResource<T>(string? containerId, string name, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var parentId = containerId ?? RootContainerPointer.Identifier;
        if (!idMap.TryGetPath(parentId, out var parentPath))
        {
            throw new DirectoryNotFoundException($"Container '{parentId}' not found.");
        }

        if (typeof(IWopiFile).IsAssignableFrom(typeof(T)))
        {
            var path = string.IsNullOrEmpty(parentPath) ? name : parentPath + "/" + name;
            var blobClient = containerClient.GetBlobClient(path);
            try
            {
                using var empty = new MemoryStream(Array.Empty<byte>(), writable: false);
                await blobClient.UploadAsync(empty, overwrite: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                throw new ArgumentException($"File '{path}' already exists.", nameof(name));
            }
            var id = idMap.Add(path);
            return await WopiBlobFile.CreateAsync(blobClient, path, id, cancellationToken).ConfigureAwait(false) as T;
        }
        if (typeof(IWopiFolder).IsAssignableFrom(typeof(T)))
        {
            var path = string.IsNullOrEmpty(parentPath) ? name : parentPath + "/" + name;
            var markerPath = path + "/" + BlobIdMap.FolderMarker;
            var markerClient = containerClient.GetBlobClient(markerPath);
            try
            {
                using var empty = new MemoryStream(Array.Empty<byte>(), writable: false);
                await markerClient.UploadAsync(empty, overwrite: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                throw new ArgumentException($"Folder '{path}' already exists.", nameof(name));
            }
            var id = idMap.Add(path);
            return new WopiBlobFolder(path, id) as T;
        }
        throw new NotSupportedException($"Unsupported resource type: {typeof(T).Name}");
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!idMap.TryGetPath(identifier, out var path))
        {
            return false;
        }

        if (typeof(IWopiFile).IsAssignableFrom(typeof(T)))
        {
            var blobClient = containerClient.GetBlobClient(path);
            var deleted = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (deleted.Value)
            {
                idMap.Remove(identifier);
            }
            return deleted.Value;
        }
        if (typeof(IWopiFolder).IsAssignableFrom(typeof(T)))
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("Cannot delete the root container.");
            }
            var prefix = path + "/";

            // Check the folder is empty (other than possibly the marker blob).
            await foreach (var item in containerClient.GetBlobsAsync(traits: BlobTraits.None, states: BlobStates.None, prefix: prefix, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (item.Name != prefix + BlobIdMap.FolderMarker)
                {
                    throw new InvalidOperationException($"Folder '{path}' is not empty.");
                }
            }

            // Delete the marker if present, then drop the id.
            var markerClient = containerClient.GetBlobClient(prefix + BlobIdMap.FolderMarker);
            await markerClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            idMap.Remove(identifier);
            return true;
        }
        throw new NotSupportedException($"Unsupported resource type: {typeof(T).Name}");
    }

    /// <inheritdoc/>
    public async Task<bool> RenameWopiResource<T>(string identifier, string requestedName, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        if (!await CheckValidName<T>(requestedName, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException("Invalid characters in the name.", nameof(requestedName));
        }
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!idMap.TryGetPath(identifier, out var oldPath))
        {
            return false;
        }
        var parent = GetParentPath(oldPath);

        if (typeof(IWopiFile).IsAssignableFrom(typeof(T)))
        {
            var newPath = string.IsNullOrEmpty(parent) ? requestedName : parent + "/" + requestedName;
            await CopyAndDeleteBlobAsync(oldPath, newPath, cancellationToken).ConfigureAwait(false);
            idMap.Update(identifier, newPath);
            return true;
        }
        if (typeof(IWopiFolder).IsAssignableFrom(typeof(T)))
        {
            if (string.IsNullOrEmpty(oldPath))
            {
                throw new InvalidOperationException("Cannot rename the root container.");
            }
            var oldPrefix = oldPath + "/";
            var newPath = string.IsNullOrEmpty(parent) ? requestedName : parent + "/" + requestedName;
            var newPrefix = newPath + "/";

            // Plain blob storage has no atomic rename — copy each blob then delete the original.
            // Order matters for crash safety: copy first, only delete after the copy succeeded.
            var renamed = new List<(string oldName, string newName, string? oldId)>();
            await foreach (var item in containerClient.GetBlobsAsync(traits: BlobTraits.None, states: BlobStates.None, prefix: oldPrefix, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                var rel = item.Name[oldPrefix.Length..];
                var dest = newPrefix + rel;
                await CopyAndDeleteBlobAsync(item.Name, dest, cancellationToken).ConfigureAwait(false);
                idMap.TryGetFileId(item.Name, out var blobId);
                renamed.Add((item.Name, dest, blobId));
            }

            // Update the id map: keep the folder identifier stable, re-point children to new paths.
            idMap.Update(identifier, newPath);
            foreach (var (_, newName, oldId) in renamed)
            {
                if (oldId is not null)
                {
                    idMap.Update(oldId, newName);
                }
            }
            return true;
        }
        throw new NotSupportedException($"Unsupported resource type: {typeof(T).Name}");
    }

    #endregion

    private string ResolveFolderPath(string identifier)
    {
        if (idMap.TryGetPath(identifier, out var path))
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
        var source = containerClient.GetBlobClient(sourcePath);
        var dest = containerClient.GetBlobClient(destPath);

        if (await dest.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Target blob '{destPath}' already exists.");
        }

        var copyOp = await dest.StartCopyFromUriAsync(source.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
        await copyOp.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
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

    private static Func<string, bool>? BuildSearchMatcher(string? searchPattern)
    {
        if (string.IsNullOrEmpty(searchPattern) || searchPattern == "*" || searchPattern == "*.*")
        {
            return null;
        }
        // Translate the simple glob (FileSystemProvider semantics) into a regex anchored to full names.
        var escaped = Regex.Escape(searchPattern).Replace("\\*", ".*").Replace("\\?", ".");
        var regex = new Regex("^" + escaped + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return name => regex.IsMatch(name);
    }
}
