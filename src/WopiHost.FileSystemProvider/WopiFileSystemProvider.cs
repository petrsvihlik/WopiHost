using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Provides files and folders based on a base64-encoded paths.
/// </summary>
public class WopiFileSystemProvider : IWopiStorageProvider, IWopiWritableStorageProvider
{
    private readonly InMemoryFileIds fileIds;
    private readonly string wopiAbsolutePath;

    /// <summary>
    /// Reference to the root container.
    /// </summary>
    public IWopiFolder RootContainerPointer { get; }

    /// <summary>
    /// Creates a new instance of the <see cref="WopiFileSystemProvider"/> based on the provided hosting environment and configuration.
    /// </summary>
    /// <param name="fileIds">In-memory storage for file identifiers.</param>
    /// <param name="env">Provides information about the hosting environment an application is running in.</param>
    /// <param name="configuration">Application configuration.</param>
    public WopiFileSystemProvider(
        InMemoryFileIds fileIds,
        IHostEnvironment env, 
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        this.fileIds = fileIds ?? throw new ArgumentNullException(nameof(fileIds));
        var fileSystemProviderOptions = configuration.GetSection(WopiConfigurationSections.STORAGE_OPTIONS)?
            .Get<WopiFileSystemProviderOptions>() ?? throw new ArgumentNullException(nameof(configuration));

        var wopiRootPath = fileSystemProviderOptions.RootPath;
        wopiAbsolutePath = Path.IsPathRooted(wopiRootPath)
            ? wopiRootPath
            : new DirectoryInfo(Path.Combine(env.ContentRootPath, wopiRootPath)).FullName;

        if (!fileIds.WasScanned)
        {
            fileIds.ScanAll(wopiAbsolutePath);
        }
        if (!fileIds.TryGetFileId(wopiAbsolutePath, out var rootId))
        {
            throw new InvalidOperationException("Root directory not found.");
        }
        RootContainerPointer = new WopiFolder(wopiAbsolutePath, rootId);
    }

    /// <inheritdoc/>
    public Task<IWopiFile?> GetWopiFile(string identifier, CancellationToken cancellationToken = default)
    {
        if (fileIds.TryGetPath(identifier, out var fullPath))
        {
            return Task.FromResult<IWopiFile?>(new WopiFile(fullPath, identifier));
        }
        return Task.FromResult<IWopiFile?>(null);
    }

    /// <inheritdoc/>
    public Task<IWopiFolder?> GetWopiContainer(string? identifier = null, CancellationToken cancellationToken = default)
    {
        identifier ??= RootContainerPointer.Identifier;
        if (fileIds.TryGetPath(identifier, out var fullPath))
        {
            return Task.FromResult<IWopiFolder?>(new WopiFolder(fullPath, identifier));
        }
        return Task.FromResult<IWopiFolder?>(null);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFile> GetWopiFiles(
        string? identifier = null, 
        string? searchPattern = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var folderPath = string.IsNullOrWhiteSpace(identifier)
            ? wopiAbsolutePath
            : fileIds.GetPath(identifier) ?? throw new DirectoryNotFoundException($"Directory '{identifier}' not found");

        foreach (var path in Directory.GetFiles(Path.Combine(wopiAbsolutePath, folderPath), searchPattern ?? "*.*"))
        {
            var filePath = Path.Combine(folderPath, Path.GetFileName(path));
            if (fileIds.TryGetFileId(filePath, out var fileId))
            {
                var result = await GetWopiFile(fileId, cancellationToken)
                    ?? throw new FileNotFoundException($"File '{fileId}' not found");
                yield return result;
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFolder> GetWopiContainers(
        string? identifier = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var folderPath = string.IsNullOrWhiteSpace(identifier)
            ? wopiAbsolutePath
            : fileIds.GetPath(identifier) ?? throw new DirectoryNotFoundException($"Directory '{identifier}' not found");

        foreach (var directory in Directory.GetDirectories(Path.Combine(wopiAbsolutePath, folderPath)))
        {
            if (fileIds.TryGetFileId(directory, out var folderId))
            {
                var result = await GetWopiContainer(folderId, cancellationToken)
                    ?? throw new DirectoryNotFoundException($"Directory '{folderId}' not found");
                yield return result;
            }
        }
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<ReadOnlyCollection<IWopiFolder>> GetAncestors(WopiResourceType resourceType, string identifier, CancellationToken cancellationToken = default)
    {
        // convert File identifier to it's parent Container's identifier
        var result = new List<IWopiFolder>();
        if (resourceType == WopiResourceType.File)
        {
            identifier = GetFileParentIdentifier(identifier);
        }
        var container = await GetWopiContainer(identifier, cancellationToken)
            ?? throw new DirectoryNotFoundException($"Directory '{identifier}' not found.");
        if (container.Identifier == RootContainerPointer.Identifier && resourceType == WopiResourceType.File)
        {
            result.Add(container);
        }

        while (container.Identifier != RootContainerPointer.Identifier)
        {
            var parentId = GetFolderParentIdentifier(container.Identifier);
            container = await GetWopiContainer(parentId, cancellationToken)
                ?? throw new DirectoryNotFoundException($"Directory '{parentId}' not found.");
            result.Add(container);
        }
        result.Reverse();
        return result.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IWopiResource?> GetWopiResourceByName(
        WopiResourceType resourceType, 
        string containerId, 
        string name, 
        CancellationToken cancellationToken = default)
    {
        if (!fileIds.TryGetPath(containerId, out var dirPath))
        {
            throw new DirectoryNotFoundException($"Directory '{containerId}' not found.");
        }
        if (!fileIds.TryGetFileId(Path.Combine(dirPath, name), out var nameId))
        {
            return null;
        }

        IWopiResource? result = resourceType switch
        {
            WopiResourceType.File => await GetWopiFile(nameId, cancellationToken),
            WopiResourceType.Container => await GetWopiContainer(nameId, cancellationToken),
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
        return result;
    }

    #region IWopiWritableStorageProvider

    /// <inheritdoc/>
    public int FileNameMaxLength { get; } = 250; // Windows limit

    /// <inheritdoc/>
    public Task<bool> CheckValidName(
        WopiResourceType resourceType,
        string name,
        CancellationToken cancellationToken = default)
    {
        return resourceType switch
        {
            WopiResourceType.File => Task.FromResult(name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name.Length < FileNameMaxLength),
            WopiResourceType.Container => Task.FromResult(name.IndexOfAny(Path.GetInvalidPathChars()) < 0),
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
    }

    /// <inheritdoc/>
    public async Task<string> GetSuggestedName(
        WopiResourceType resourceType,
        string containerId,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (!await CheckValidName(resourceType, name, cancellationToken))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(name));
        }
        if (!fileIds.TryGetPath(containerId, out var fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{containerId}' not found.");
        }
        var newPath = Path.Combine(fullPath, name);

        // are we trying to create a container?
        if (resourceType == WopiResourceType.Container)
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
        else if (resourceType == WopiResourceType.File)
        {
            if (File.Exists(newPath))
            {
                var newName = name;
                var counter = 1;
                while (File.Exists(Path.Combine(fullPath, newName)))
                {
                    newName = $"{Path.GetFileNameWithoutExtension(name)} ({counter++}) {Path.GetExtension(name)}";
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
    public async Task<IWopiResource?> CreateWopiChildResource(
        WopiResourceType resourceType,
        string? containerId,
        string name,
        CancellationToken cancellationToken = default)
    {
        return resourceType switch
        {
            WopiResourceType.File => await CreateWopiFile(containerId ?? RootContainerPointer.Identifier, name, cancellationToken),
            WopiResourceType.Container => await CreateWopiChildContainer(containerId ?? RootContainerPointer.Identifier, name, cancellationToken),
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
    }

    private Task<IWopiFile?> CreateWopiFile(
        string containerId,
        string name,
        CancellationToken cancellationToken)
    {
        if (!fileIds.TryGetPath(containerId, out var fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{containerId}' found.");
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

        var newFileId = fileIds.AddFile(newPath);
        return GetWopiFile(newFileId, cancellationToken);
    }

    private Task<IWopiFolder?> CreateWopiChildContainer(
        string containerId,
        string name,
        CancellationToken cancellationToken)
    {
        if (!fileIds.TryGetPath(containerId, out var fullPath))
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
        var newId = fileIds.AddFile(dirInfo.FullName);
        return GetWopiContainer(newId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteWopiResource(WopiResourceType resourceType, string identifier, CancellationToken cancellationToken = default)
    {
        var result = resourceType switch
        {
            WopiResourceType.File => DeleteWopiFile(identifier),
            WopiResourceType.Container => DeleteWopiContainer(identifier),
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
        return Task.FromResult<bool>(result);
    }

    private bool DeleteWopiContainer(string identifier)
    {
        if (!fileIds.TryGetPath(identifier, out var fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{identifier}' not found.");
        }
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{fullPath}' not found");
        }
        if (Directory.EnumerateFileSystemEntries(fullPath).Any())
        {
            throw new InvalidOperationException($"Directory '{fullPath}' is not empty.");
        }
        Directory.Delete(fullPath, true);
        fileIds.RemoveId(identifier);
        return true;
    }

    private bool DeleteWopiFile(string identifier)
    {
        if (!fileIds.TryGetPath(identifier, out var fullPath))
        {
            throw new FileNotFoundException($"File '{identifier}' not found.");
        }
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File '{fullPath}' not found");
        }
        File.Delete(fullPath);
        fileIds.RemoveId(identifier);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RenameWopiResource(WopiResourceType resourceType, string identifier, string requestedName, CancellationToken cancellationToken = default)
    {
        if (!await CheckValidName(resourceType, requestedName, cancellationToken))
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(requestedName));
        }

        if (resourceType == WopiResourceType.File)
        {
            if (!fileIds.TryGetPath(identifier, out var fullPath))
            {
                throw new FileNotFoundException($"File '{identifier}' not found.");
            }
            var parentPath = Path.GetDirectoryName(fullPath)
                ?? throw new DirectoryNotFoundException("Parent directory not found");
            var newPath = Path.Combine(parentPath, requestedName);
            if (File.Exists(newPath))
            {
                throw new InvalidOperationException($"Target File '{newPath}' already exists.");
            }
            File.Move(fullPath, newPath);
            fileIds.UpdateFile(identifier, newPath);
        }
        else if (resourceType == WopiResourceType.Container)
        {
            if (!fileIds.TryGetPath(identifier, out var fullPath))
            {
                throw new FileNotFoundException($"Directory '{identifier}'not found.");
            }
            var parentPath = Path.GetDirectoryName(fullPath)
                ?? throw new DirectoryNotFoundException("Parent directory not found");
            var newPath = Path.Combine(parentPath, requestedName);
            if (Directory.Exists(newPath))
            {
                throw new InvalidOperationException($"Target Directory '{newPath}' already exists.");
            }
            Directory.Move(fullPath, newPath);
            fileIds.UpdateFile(identifier, newPath);
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
        if (!fileIds.TryGetPath(identifier, out var filePath))
        {
            throw new FileNotFoundException($"File '{identifier}' not found");
        }
        var parentPath = Path.GetDirectoryName(filePath)
            ?? throw new DirectoryNotFoundException("Parent directory not found");
        if (!fileIds.TryGetFileId(parentPath, out var parentFolderId))
        {
            throw new DirectoryNotFoundException("Parent directory not found");
        }
        return parentFolderId;
    }

    private string GetFolderParentIdentifier(string identifier)
    {
        if (!fileIds.TryGetPath(identifier, out var folderPath))
        {
            throw new DirectoryNotFoundException($"File '{identifier}' not found");
        }
        var parentPath = Path.GetDirectoryName(folderPath)
            ?? throw new DirectoryNotFoundException("Parent directory not found");

        if (!fileIds.TryGetFileId(parentPath, out var parentFolderId))
        {
            throw new DirectoryNotFoundException("Parent directory not found");
        }
        return parentFolderId;
    }
}