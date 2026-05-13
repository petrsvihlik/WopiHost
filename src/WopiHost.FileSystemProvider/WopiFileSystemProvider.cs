using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Provides files and folders based on a base64-encoded paths.
/// </summary>
public partial class WopiFileSystemProvider : IWopiStorageProvider, IWopiWritableStorageProvider
{
    private readonly InMemoryFileIds _fileIds;
    private readonly string _wopiAbsolutePath;
    private readonly ILogger<WopiFileSystemProvider> _logger;

    /// <inheritdoc />
    public IWopiFolder RootContainer { get; }

    /// <summary>
    /// Creates a new instance of the <see cref="WopiFileSystemProvider"/> based on the provided hosting environment and configuration.
    /// </summary>
    /// <param name="fileIds">In-memory storage for file identifiers.</param>
    /// <param name="env">Provides information about the hosting environment an application is running in.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">Logger.</param>
    public WopiFileSystemProvider(
        InMemoryFileIds fileIds,
        IHostEnvironment env,
        IConfiguration configuration,
        ILogger<WopiFileSystemProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _fileIds = fileIds ?? throw new ArgumentNullException(nameof(fileIds));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var fileSystemProviderOptions = configuration.GetSection(WopiFileSystemProviderOptions.SectionName)?
            .Get<WopiFileSystemProviderOptions>() ?? throw new ArgumentNullException(nameof(configuration));

        var wopiRootPath = fileSystemProviderOptions.RootPath;
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
        RootContainer = new WopiFolder(_wopiAbsolutePath, rootId);
        LogProviderInitialized(_logger, _wopiAbsolutePath);
    }

    /// <inheritdoc/>
    public Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        if (_fileIds.TryGetPath(identifier, out var fullPath))
        {
            return typeof(T) switch
            {
                { } wopiFileType when typeof(IWopiFile).IsAssignableFrom(wopiFileType) => Task.FromResult(new WopiFile(fullPath, identifier) as T),
                { } wopiFolderType when typeof(IWopiFolder).IsAssignableFrom(wopiFolderType) => Task.FromResult(new WopiFolder(fullPath, identifier) as T),
                _ => throw new NotSupportedException($"Unsupported resource type: {typeof(T).Name}"),
            };
        }
        return Task.FromResult<T?>(null);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFile> GetWopiFiles(
        string identifier,
        IReadOnlyCollection<string>? fileExtensions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        var folderPath = _fileIds.GetPath(identifier)
            ?? throw new DirectoryNotFoundException($"Directory '{identifier}' not found");

        var absolutePath = Path.Combine(_wopiAbsolutePath, folderPath);

        // Push the extension filter into the OS-level enumeration: one Directory.EnumerateFiles
        // call per requested extension, each with its own glob. Distinct extensions produce
        // disjoint result sets so no dedup is needed when we concatenate. With no filter, fall
        // back to a single unfiltered enumeration.
        //
        // MatchCasing.CaseInsensitive is explicit because the EnumerationOptions default is
        // PlatformDefault, which means case-sensitive matching on Linux. WOPI requires
        // case-insensitive extension matching, so we force it here regardless of host OS.
        var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };
        var enumeration = (fileExtensions is { Count: > 0 })
            ? fileExtensions.SelectMany(ext => Directory.EnumerateFiles(absolutePath, "*" + ext, options))
            : Directory.EnumerateFiles(absolutePath, "*", options);

        foreach (var path in enumeration)
        {
            var filePath = Path.Combine(folderPath, Path.GetFileName(path));
            if (_fileIds.TryGetFileId(filePath, out var fileId))
            {
                var result = await GetWopiResource<IWopiFile>(fileId, cancellationToken).ConfigureAwait(false)
                    ?? throw new FileNotFoundException($"File '{fileId}' not found");
                yield return result;
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFolder> GetWopiContainers(
        string identifier,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        var folderPath = _fileIds.GetPath(identifier)
            ?? throw new DirectoryNotFoundException($"Directory '{identifier}' not found");

        foreach (var directory in Directory.GetDirectories(Path.Combine(_wopiAbsolutePath, folderPath)))
        {
            if (_fileIds.TryGetFileId(directory, out var folderId))
            {
                var result = await GetWopiResource<IWopiFolder>(folderId, cancellationToken).ConfigureAwait(false)
                    ?? throw new DirectoryNotFoundException($"Directory '{folderId}' not found");
                yield return result;
            }
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ReadOnlyCollection<IWopiFolder>> GetAncestors<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        // convert File identifier to it's parent Container's identifier
        var result = new List<IWopiFolder>();
        if (typeof(T) == typeof(IWopiFile))
        {
            identifier = GetFileParentIdentifier(identifier);
        }
        var container = await GetWopiResource<IWopiFolder>(identifier, cancellationToken).ConfigureAwait(false)
            ?? throw new DirectoryNotFoundException($"Directory '{identifier}' not found.");
        if (typeof(T) == typeof(IWopiFile))
        {
            // For files, the immediate parent container is always part of the
            // ancestor list (whether it is the root or a nested folder).
            result.Add(container);
        }

        while (container.Identifier != RootContainer.Identifier)
        {
            var parentId = GetFolderParentIdentifier(container.Identifier);
            container = await GetWopiResource<IWopiFolder>(parentId, cancellationToken).ConfigureAwait(false)
                ?? throw new DirectoryNotFoundException($"Directory '{parentId}' not found.");
            result.Add(container);
        }
        result.Reverse();
        return result.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<T?> GetWopiResourceByName<T>(
        string containerId,
        string name,
        CancellationToken cancellationToken = default) where T : class, IWopiResource
    {
        // Missing parent: return null (#380 item 4.2). Azure storage provider already behaves
        // this way; throwing was a FileSystemProvider-only quirk that leaked through callers.
        if (!_fileIds.TryGetPath(containerId, out var dirPath))
        {
            return default;
        }
        if (!_fileIds.TryGetFileId(Path.Combine(dirPath, name), out var nameId))
        {
            return default;
        }

        T? result = typeof(T) switch
        {
            { } wopiFileType when typeof(IWopiFile).IsAssignableFrom(wopiFileType) => await GetWopiResource<IWopiFile>(nameId, cancellationToken).ConfigureAwait(false) as T,
            { } wopiFolderType when typeof(IWopiFolder).IsAssignableFrom(wopiFolderType) => await GetWopiResource<IWopiFolder>(nameId, cancellationToken).ConfigureAwait(false) as T,
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
        return result;
    }

    #region IWopiWritableStorageProvider

    /// <inheritdoc/>
    public int FileNameMaxLength { get; } = 250; // Windows limit

    /// <inheritdoc/>
    public Task<bool> CheckValidName<T>(
        string name,
        CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        return typeof(T) switch
        {
            { } wopiFileType when typeof(IWopiFile).IsAssignableFrom(wopiFileType) => Task.FromResult(name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name.Length < FileNameMaxLength),
            { } wopiFolderType when typeof(IWopiFolder).IsAssignableFrom(wopiFolderType) => Task.FromResult(name.IndexOfAny(Path.GetInvalidPathChars()) < 0),
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
    }

    /// <inheritdoc/>
    public async Task<string> GetSuggestedName<T>(
        string containerId,
        string name,
        CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        if (!await CheckValidName<T>(name, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(name));
        }
        if (!_fileIds.TryGetPath(containerId, out var fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{containerId}' not found.");
        }
        var newPath = Path.Combine(fullPath, name);

        // are we trying to create a container?
        if (typeof(T) == typeof(IWopiFolder))
        {
            if (Directory.Exists(newPath))
            {
                var newName = name;
                var counter = 1;
                while (Directory.Exists(Path.Combine(fullPath, newName)))
                {
                    newName = $"{name} ({counter++})";
                }
                return newName;
            }
            else
            {
                return name;
            }
        }
        else if (typeof(T) == typeof(IWopiFile))
        {
            if (File.Exists(newPath))
            {
                var newName = name;
                var counter = 1;
                while (File.Exists(Path.Combine(fullPath, newName)))
                {
                    newName = $"{Path.GetFileNameWithoutExtension(name)} ({counter++}){Path.GetExtension(name)}";
                }
                return newName;
            }
            else
            {
                return name;
            }
        }
        else
        {
            throw new NotSupportedException("Unsupported resource type.");
        }
    }

    /// <inheritdoc/>
    public async Task<T?> CreateWopiChildResource<T>(
        string containerId,
        string name,
        CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        ArgumentNullException.ThrowIfNull(containerId);
        return typeof(T) switch
        {
            { } wopiFileType when typeof(IWopiFile).IsAssignableFrom(wopiFileType) => await CreateWopiFile(containerId, name, cancellationToken).ConfigureAwait(false) as T,
            { } wopiFolderType when typeof(IWopiFolder).IsAssignableFrom(wopiFolderType) => await CreateWopiChildContainer(containerId, name, cancellationToken).ConfigureAwait(false) as T,
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
    }

    private Task<IWopiFile?> CreateWopiFile(
        string containerId,
        string name,
        CancellationToken cancellationToken)
    {
        if (!_fileIds.TryGetPath(containerId, out var fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{containerId}' not found.");
        }
        var newPath = Path.Combine(fullPath, name);
        if (File.Exists(newPath))
        {
            throw new ArgumentException($"File '{newPath}' already exists.", nameof(name));
        }

        // Create an empty file
        using (var fs = new FileStream(newPath, FileMode.CreateNew))
        {
            // Create a 0-byte file
        }

        var newFileId = _fileIds.AddFile(newPath);
        LogFileCreated(_logger, newFileId, newPath);
        return GetWopiResource<IWopiFile>(newFileId, cancellationToken);
    }

    private Task<IWopiFolder?> CreateWopiChildContainer(
        string containerId,
        string name,
        CancellationToken cancellationToken)
    {
        if (!_fileIds.TryGetPath(containerId, out var fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{containerId}' not found.");
        }

        var newPath = Path.Combine(fullPath, name);
        var dirInfo = new DirectoryInfo(newPath);
        if (dirInfo.Exists)
        {
            throw new ArgumentException($"Directory '{newPath}' already exists.", nameof(name));
        }
        else
        {
            dirInfo.Create();
        }
        var newId = _fileIds.AddFile(dirInfo.FullName);
        LogFolderCreated(_logger, newId, dirInfo.FullName);
        return GetWopiResource<IWopiFolder>(newId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        var result = typeof(T) switch
        {
            { } wopiFileType when typeof(IWopiFile).IsAssignableFrom(wopiFileType) => DeleteWopiFile(identifier),
            { } wopiFolderType when typeof(IWopiFolder).IsAssignableFrom(wopiFolderType) => DeleteWopiContainer(identifier),
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
        return Task.FromResult<bool>(result);
    }

    private bool DeleteWopiContainer(string identifier)
    {
        // Missing identifier → return false (#380 item 4.2). The non-empty case still throws —
        // that's the WOPI 409 path, distinct from 404.
        if (!_fileIds.TryGetPath(identifier, out var fullPath) || !Directory.Exists(fullPath))
        {
            return false;
        }
        if (Directory.EnumerateFileSystemEntries(fullPath).Any())
        {
            throw new InvalidOperationException($"Directory '{fullPath}' is not empty.");
        }
        Directory.Delete(fullPath, true);
        _fileIds.RemoveId(identifier);
        LogFolderDeleted(_logger, identifier, fullPath);
        return true;
    }

    private bool DeleteWopiFile(string identifier)
    {
        // Missing identifier → return false (#380 item 4.2).
        if (!_fileIds.TryGetPath(identifier, out var fullPath) || !File.Exists(fullPath))
        {
            return false;
        }
        File.Delete(fullPath);
        _fileIds.RemoveId(identifier);
        LogFileDeleted(_logger, identifier, fullPath);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RenameWopiResource<T>(string identifier, string requestedName, CancellationToken cancellationToken = default)
        where T : class, IWopiResource
    {
        if (!await CheckValidName<T>(requestedName, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(requestedName));
        }

        if (typeof(T) == typeof(IWopiFile))
        {
            // Missing identifier → return false (#380 item 4.2). The target-already-exists
            // case still throws — that's the WOPI 409 / X-WOPI-InvalidFileNameError path.
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
        }
        else if (typeof(T) == typeof(IWopiFolder))
        {
            // Missing identifier → return false (#380 item 4.2).
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
        }
        else
        {
            throw new NotSupportedException("Unsupported resource type.");
        }

        return true;
    }
    #endregion

    private string GetFileParentIdentifier(string identifier)
    {
        if (!_fileIds.TryGetPath(identifier, out var filePath))
        {
            throw new FileNotFoundException($"File '{identifier}' not found");
        }
        var parentPath = Path.GetDirectoryName(filePath)
            ?? throw new DirectoryNotFoundException("Parent directory not found");
        if (!_fileIds.TryGetFileId(parentPath, out var parentFolderId))
        {
            throw new DirectoryNotFoundException("Parent directory not found");
        }
        return parentFolderId;
    }

    private string GetFolderParentIdentifier(string identifier)
    {
        if (!_fileIds.TryGetPath(identifier, out var folderPath))
        {
            throw new DirectoryNotFoundException($"Folder '{identifier}' not found");
        }
        var parentPath = Path.GetDirectoryName(folderPath)
            ?? throw new DirectoryNotFoundException("Parent directory not found");

        if (!_fileIds.TryGetFileId(parentPath, out var parentFolderId))
        {
            throw new DirectoryNotFoundException("Parent directory not found");
        }
        return parentFolderId;
    }
}