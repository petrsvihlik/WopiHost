using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Provides files and folders from a local directory tree, addressing each resource by a
/// deterministic SHA-256 hash of its canonical path (see <see cref="InMemoryFileIds"/>).
/// </summary>
public partial class WopiFileSystemProvider : IWopiStorageProvider, IWopiWritableStorageProvider
{
    private readonly InMemoryFileIds _fileIds;
    private readonly string _wopiAbsolutePath;
    private readonly ILogger<WopiFileSystemProvider> _logger;

    /// <inheritdoc />
    public IWopiContainer RootContainer { get; }

    /// <summary>
    /// Creates a new instance of the <see cref="WopiFileSystemProvider"/> based on the provided hosting environment and configuration.
    /// </summary>
    /// <param name="fileIds">In-memory storage for file identifiers.</param>
    /// <param name="env">Provides information about the hosting environment an application is running in.</param>
    /// <param name="options">Bound file-system provider options, supplying <c>RootPath</c>.</param>
    /// <param name="logger">Logger.</param>
    public WopiFileSystemProvider(
        InMemoryFileIds fileIds,
        IHostEnvironment env,
        IOptions<WopiFileSystemProviderOptions> options,
        ILogger<WopiFileSystemProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(options);
        _fileIds = fileIds ?? throw new ArgumentNullException(nameof(fileIds));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var wopiRootPath = options.Value.RootPath;
        _wopiAbsolutePath = Path.IsPathRooted(wopiRootPath)
            ? wopiRootPath
            : new DirectoryInfo(Path.Combine(env.ContentRootPath, wopiRootPath)).FullName;

        if (!_fileIds.WasScanned)
        {
            _fileIds.ScanAll(_wopiAbsolutePath);
        }
        if (!_fileIds.TryGetFileId(_wopiAbsolutePath, out var rootId))
        {
            throw new InvalidOperationException("Root directory not found.");
        }
        RootContainer = new WopiContainer(_wopiAbsolutePath, rootId);
        LogProviderInitialized(_logger, _wopiAbsolutePath);
    }

    /// <inheritdoc/>
    public Task<IWopiFile?> GetWopiFile(string identifier, CancellationToken cancellationToken = default)
    {
        if (TryResolvePath(identifier, out var fullPath))
        {
            return Task.FromResult<IWopiFile?>(new WopiFile(fullPath, identifier));
        }
        return Task.FromResult<IWopiFile?>(null);
    }

    /// <inheritdoc/>
    public Task<IWopiWritableFile?> GetWritableFile(string identifier, CancellationToken cancellationToken = default)
    {
        // Same WopiFile the read-side returns — the concrete class implements IWopiWritableFile
        // (which extends IWopiFile), so read vs. writable is purely the static type the caller sees.
        if (TryResolvePath(identifier, out var fullPath))
        {
            return Task.FromResult<IWopiWritableFile?>(new WopiFile(fullPath, identifier));
        }
        return Task.FromResult<IWopiWritableFile?>(null);
    }

    /// <inheritdoc/>
    public Task<IWopiContainer?> GetWopiContainer(string identifier, CancellationToken cancellationToken = default)
    {
        if (TryResolvePath(identifier, out var fullPath))
        {
            return Task.FromResult<IWopiContainer?>(new WopiContainer(fullPath, identifier));
        }
        return Task.FromResult<IWopiContainer?>(null);
    }

    /// <summary>
    /// Map lookup with a scan-on-miss fallback. Ids derive deterministically from paths, so an
    /// id this process has never seen — minted by another process over the same tree after a
    /// rename or create — is recoverable by hashing the on-disk entries.
    /// </summary>
    private bool TryResolvePath(string identifier, [NotNullWhen(true)] out string? fullPath)
        => _fileIds.TryGetPath(identifier, out fullPath)
            || _fileIds.TryResolveByScan(_wopiAbsolutePath, identifier, out fullPath);

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFile> GetWopiFiles(
        string identifier,
        IReadOnlyCollection<string>? fileExtensions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        if (!TryResolvePath(identifier, out var folderPath))
        {
            throw new DirectoryNotFoundException($"Directory '{identifier}' not found");
        }

        var absolutePath = Path.Combine(_wopiAbsolutePath, folderPath);

        // Push the extension filter into the OS-level enumeration: one Directory.EnumerateFiles
        // call per requested extension, each with its own glob. Distinct extensions produce
        // disjoint result sets so the concatenation needs no dedup. With no filter, a single
        // unfiltered enumeration is used.
        //
        // MatchCasing.CaseInsensitive is explicit because the EnumerationOptions default is
        // PlatformDefault, which means case-sensitive matching on Linux. WOPI requires
        // case-insensitive extension matching, so it is forced here regardless of host OS.
        var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };
        var enumeration = (fileExtensions is { Count: > 0 })
            ? fileExtensions.SelectMany(ext => Directory.EnumerateFiles(absolutePath, "*" + ext, options))
            : Directory.EnumerateFiles(absolutePath, "*", options);

        foreach (var path in enumeration)
        {
            var filePath = Path.Combine(folderPath, Path.GetFileName(path));
            // The startup scan can't have seen entries created or renamed by another process
            // sharing the tree; ids derive deterministically from paths, so register such a path
            // lazily instead of hiding the file from the listing.
            var fileId = _fileIds.GetOrAddFileId(filePath);
            var result = await GetWopiFile(fileId, cancellationToken).ConfigureAwait(false)
                ?? throw new FileNotFoundException($"File '{fileId}' not found");
            yield return result;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiContainer> GetWopiContainers(
        string identifier,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        if (!TryResolvePath(identifier, out var folderPath))
        {
            throw new DirectoryNotFoundException($"Directory '{identifier}' not found");
        }

        foreach (var directory in Directory.GetDirectories(Path.Combine(_wopiAbsolutePath, folderPath)))
        {
            // Same lazy registration as GetWopiFiles — a folder created or renamed by another
            // process must still show up in the listing.
            var folderId = _fileIds.GetOrAddFileId(directory);
            var result = await GetWopiContainer(folderId, cancellationToken).ConfigureAwait(false)
                ?? throw new DirectoryNotFoundException($"Directory '{folderId}' not found");
            yield return result;
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ReadOnlyCollection<IWopiContainer>> GetFileAncestors(string fileId, CancellationToken cancellationToken = default)
    {
        var parentId = GetFileParentIdentifier(fileId);
        var result = new List<IWopiContainer>();
        var container = await GetWopiContainer(parentId, cancellationToken).ConfigureAwait(false)
            ?? throw new DirectoryNotFoundException($"Directory '{parentId}' not found.");
        // For files, the immediate parent container is always part of the ancestor list (whether
        // it is the root or a nested folder).
        result.Add(container);
        await WalkAncestorsAsync(container, result, cancellationToken).ConfigureAwait(false);
        result.Reverse();
        return result.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<ReadOnlyCollection<IWopiContainer>> GetContainerAncestors(string containerId, CancellationToken cancellationToken = default)
    {
        var result = new List<IWopiContainer>();
        var container = await GetWopiContainer(containerId, cancellationToken).ConfigureAwait(false)
            ?? throw new DirectoryNotFoundException($"Directory '{containerId}' not found.");
        await WalkAncestorsAsync(container, result, cancellationToken).ConfigureAwait(false);
        result.Reverse();
        return result.AsReadOnly();
    }

    private async Task WalkAncestorsAsync(IWopiContainer container, List<IWopiContainer> result, CancellationToken cancellationToken)
    {
        while (container.Identifier != RootContainer.Identifier)
        {
            var parentId = GetFolderParentIdentifier(container.Identifier);
            container = await GetWopiContainer(parentId, cancellationToken).ConfigureAwait(false)
                ?? throw new DirectoryNotFoundException($"Directory '{parentId}' not found.");
            result.Add(container);
        }
    }

    /// <inheritdoc/>
    public async Task<IWopiFile?> GetWopiFileByName(
        string containerId,
        string name,
        CancellationToken cancellationToken = default)
    {
        // Missing parent: return null.
        if (!TryResolvePath(containerId, out var dirPath))
        {
            return null;
        }
        // name is client-controlled in some flows (PutRelativeFile target negotiation). Now that
        // unknown on-disk paths register lazily, a rooted or '..' name would resolve and escape
        // the container — reject anything that isn't a single path segment before combining.
        if (!IsValidSingleSegmentName(name))
        {
            return null;
        }
        var candidatePath = Path.Combine(dirPath, name);
        if (!_fileIds.TryGetFileId(candidatePath, out var nameId))
        {
            // A file on disk that the startup scan never saw still has a derivable id.
            if (!File.Exists(candidatePath))
            {
                return null;
            }
            nameId = _fileIds.GetOrAddFileId(candidatePath);
        }
        return await GetWopiFile(nameId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IWopiContainer?> GetWopiContainerByName(
        string containerId,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolvePath(containerId, out var dirPath))
        {
            return null;
        }
        // Same single-segment guard as GetWopiFileByName — lazy registration must stay
        // confined to the container.
        if (!IsValidSingleSegmentName(name))
        {
            return null;
        }
        var candidatePath = Path.Combine(dirPath, name);
        if (!_fileIds.TryGetFileId(candidatePath, out var nameId))
        {
            if (!Directory.Exists(candidatePath))
            {
                return null;
            }
            nameId = _fileIds.GetOrAddFileId(candidatePath);
        }
        return await GetWopiContainer(nameId, cancellationToken).ConfigureAwait(false);
    }

    #region IWopiWritableStorageProvider

    /// <inheritdoc/>
    public int FileNameMaxLength { get; } = 250; // Windows limit

    /// <inheritdoc/>
    public Task<bool> CheckValidFileName(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(IsValidSingleSegmentName(name));

    /// <inheritdoc/>
    public Task<bool> CheckValidContainerName(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(IsValidSingleSegmentName(name));

    // Unified validation for files and containers — on a file-system store the two share the
    // same namespace (a directory entry is a directory entry) and the same constraints.
    // GetInvalidFileNameChars() (not GetInvalidPathChars()) forbids the path separators that
    // would otherwise let "sub/sub" or "foo\bar" through and break downstream.
    private bool IsValidSingleSegmentName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= FileNameMaxLength
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && name != "." && name != "..";

    /// <inheritdoc/>
    public async Task<string> GetSuggestedFileName(string containerId, string name, CancellationToken cancellationToken = default)
    {
        if (!await CheckValidFileName(name, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(name));
        }
        var fullPath = ResolveContainerPath(containerId);
        var newPath = Path.Combine(fullPath, name);
        if (!File.Exists(newPath))
        {
            return name;
        }
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        var counter = 1;
        var candidate = name;
        while (File.Exists(Path.Combine(fullPath, candidate)))
        {
            candidate = $"{stem} ({counter++}){ext}";
        }
        return candidate;
    }

    /// <inheritdoc/>
    public async Task<string> GetSuggestedContainerName(string containerId, string name, CancellationToken cancellationToken = default)
    {
        if (!await CheckValidContainerName(name, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(name));
        }
        var fullPath = ResolveContainerPath(containerId);
        var newPath = Path.Combine(fullPath, name);
        if (!Directory.Exists(newPath))
        {
            return name;
        }
        var counter = 1;
        var candidate = name;
        while (Directory.Exists(Path.Combine(fullPath, candidate)))
        {
            candidate = $"{name} ({counter++})";
        }
        return candidate;
    }

    /// <inheritdoc/>
    public async Task<IWopiWritableFile?> CreateWopiChildFile(
        string containerId,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(containerId);
        // Reject names that aren't a single path segment (separators, '.', '..') before combining —
        // the name is client-controlled (relative/suggested target), so this is the path-traversal guard.
        if (!await CheckValidFileName(name, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(name));
        }
        var fullPath = ResolveContainerPath(containerId);
        var newPath = Path.Combine(fullPath, name);
        if (File.Exists(newPath))
        {
            throw new ArgumentException($"File '{newPath}' already exists.", nameof(name));
        }

        using (new FileStream(newPath, FileMode.CreateNew))
        {
        }

        var newFileId = _fileIds.AddFile(newPath);
        LogFileCreated(_logger, newFileId, newPath);
        return await GetWritableFile(newFileId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IWopiContainer?> CreateWopiChildContainer(
        string containerId,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(containerId);
        // Reject names that aren't a single path segment (separators, '.', '..') before combining —
        // the name is client-controlled, so this is the path-traversal guard.
        if (!await CheckValidContainerName(name, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(name));
        }
        var fullPath = ResolveContainerPath(containerId);
        var newPath = Path.Combine(fullPath, name);
        var dirInfo = new DirectoryInfo(newPath);
        if (dirInfo.Exists)
        {
            throw new ArgumentException($"Directory '{newPath}' already exists.", nameof(name));
        }
        dirInfo.Create();
        var newId = _fileIds.AddFile(dirInfo.FullName);
        LogFolderCreated(_logger, newId, dirInfo.FullName);
        return await GetWopiContainer(newId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteWopiFile(string identifier, CancellationToken cancellationToken = default)
    {
        // Missing identifier → return false.
        if (!_fileIds.TryGetPath(identifier, out var fullPath) || !File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }
        File.Delete(fullPath);
        _fileIds.RemoveId(identifier);
        LogFileDeleted(_logger, identifier, fullPath);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteWopiContainer(string identifier, CancellationToken cancellationToken = default)
    {
        // Missing identifier → return false. The non-empty case still throws —
        // that's the WOPI 409 path, distinct from 404.
        if (!_fileIds.TryGetPath(identifier, out var fullPath) || !Directory.Exists(fullPath))
        {
            return Task.FromResult(false);
        }
        if (Directory.EnumerateFileSystemEntries(fullPath).Any())
        {
            throw new InvalidOperationException($"Directory '{fullPath}' is not empty.");
        }
        Directory.Delete(fullPath, true);
        _fileIds.RemoveId(identifier);
        LogFolderDeleted(_logger, identifier, fullPath);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task<bool> RenameWopiFile(string identifier, string requestedName, CancellationToken cancellationToken = default)
    {
        if (!await CheckValidFileName(requestedName, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(requestedName));
        }
        // Missing identifier → return false. The target-already-exists case still
        // throws — that's the WOPI 409 / X-WOPI-InvalidFileNameError path.
        if (!_fileIds.TryGetPath(identifier, out var fullPath) || !File.Exists(fullPath))
        {
            return false;
        }
        var parentPath = Path.GetDirectoryName(fullPath)
            ?? throw new DirectoryNotFoundException("Parent directory not found");
        var newPath = Path.Combine(parentPath, requestedName);
        if (File.Exists(newPath))
        {
            throw new InvalidOperationException($"Target File '{newPath}' already exists.");
        }
        File.Move(fullPath, newPath);
        _fileIds.UpdateFile(identifier, newPath);
        LogFileRenamed(_logger, identifier, fullPath, newPath);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RenameWopiContainer(string identifier, string requestedName, CancellationToken cancellationToken = default)
    {
        if (!await CheckValidContainerName(requestedName, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(requestedName));
        }
        if (!_fileIds.TryGetPath(identifier, out var fullPath) || !Directory.Exists(fullPath))
        {
            return false;
        }
        var parentPath = Path.GetDirectoryName(fullPath)
            ?? throw new DirectoryNotFoundException("Parent directory not found");
        var newPath = Path.Combine(parentPath, requestedName);
        if (Directory.Exists(newPath))
        {
            throw new InvalidOperationException($"Target Directory '{newPath}' already exists.");
        }
        Directory.Move(fullPath, newPath);
        _fileIds.UpdateFile(identifier, newPath);
        LogFolderRenamed(_logger, identifier, fullPath, newPath);
        return true;
    }
    #endregion

    private string ResolveContainerPath(string containerId)
    {
        if (!TryResolvePath(containerId, out var fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{containerId}' not found.");
        }
        return fullPath;
    }

    private string GetFileParentIdentifier(string identifier)
    {
        if (!TryResolvePath(identifier, out var filePath))
        {
            throw new FileNotFoundException($"File '{identifier}' not found");
        }
        var parentPath = Path.GetDirectoryName(filePath)
            ?? throw new DirectoryNotFoundException("Parent directory not found");
        // A lazily-resolved file may sit in a directory the startup scan never mapped; the
        // parent of a resolved path always exists on disk, so its id is derivable.
        return _fileIds.GetOrAddFileId(parentPath);
    }

    private string GetFolderParentIdentifier(string identifier)
    {
        if (!TryResolvePath(identifier, out var folderPath))
        {
            throw new DirectoryNotFoundException($"Folder '{identifier}' not found");
        }
        var parentPath = Path.GetDirectoryName(folderPath)
            ?? throw new DirectoryNotFoundException("Parent directory not found");
        return _fileIds.GetOrAddFileId(parentPath);
    }
}
