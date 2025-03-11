using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Provides files and folders based on a base64-encoded paths.
/// </summary>
public class WopiFileSystemProvider : IWopiStorageProvider, IWopiWritableStorageProvider
{
    private WopiFileSystemProviderOptions FileSystemProviderOptions { get; }

    private readonly string rootPath = @".\";

    private string WopiRootPath => FileSystemProviderOptions.RootPath;

    private string WopiAbsolutePath => Path.IsPathRooted(WopiRootPath) 
        ? WopiRootPath 
        : new DirectoryInfo(Path.Combine(HostEnvironment.ContentRootPath, WopiRootPath)).FullName;

    /// <summary>
    /// Reference to the root container.
    /// </summary>
    public IWopiFolder RootContainerPointer => new WopiFolder(WopiAbsolutePath, EncodeIdentifier(rootPath));

    /// <summary>
    /// Context of the hosting environment.
    /// </summary>
    protected IHostEnvironment HostEnvironment { get; set; }

    /// <summary>
    /// Creates a new instance of the <see cref="WopiFileSystemProvider"/> based on the provided hosting environment and configuration.
    /// </summary>
    /// <param name="env">Provides information about the hosting environment an application is running in.</param>
    /// <param name="configuration">Application configuration.</param>
    public WopiFileSystemProvider(IHostEnvironment env, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        HostEnvironment = env ?? throw new ArgumentNullException(nameof(env));
        FileSystemProviderOptions = configuration.GetSection(WopiConfigurationSections.STORAGE_OPTIONS)?
            .Get<WopiFileSystemProviderOptions>() ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc/>
    public IWopiFile GetWopiFile(string identifier)
    {
        var fullPath = DecodeFullPath(identifier);
        return new WopiFile(fullPath, identifier);
    }

    /// <inheritdoc/>
    public IWopiFolder GetWopiContainer(string identifier = "")
    {
        var fullPath = DecodeFullPath(identifier);
        return new WopiFolder(fullPath, identifier);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFile> GetWopiFiles(
        string? identifier = null, 
        string? searchPattern = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var folderPath = DecodeIdentifier(identifier ?? string.Empty);
        foreach (var path in Directory.GetFiles(Path.Combine(WopiAbsolutePath, folderPath), searchPattern ?? "*.*"))
        {
            var filePath = Path.Combine(folderPath, Path.GetFileName(path));
            var fileId = EncodeIdentifier(filePath);
            yield return GetWopiFile(fileId);
        }
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IWopiFolder> GetWopiContainers(
        string? identifier = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var folderPath = DecodeIdentifier(identifier ?? string.Empty);
        foreach (var directory in Directory.GetDirectories(Path.Combine(WopiAbsolutePath, folderPath)))
        {
            var subfolderPath = Path.GetRelativePath(WopiAbsolutePath, directory);
            var folderId = EncodeIdentifier(subfolderPath);
            yield return GetWopiContainer(folderId);
        }
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ReadOnlyCollection<IWopiFolder>> GetAncestors(WopiResourceType resourceType, string identifier, CancellationToken cancellationToken = default)
    {
        // convert File identifier to it's parent Container's identifier
        var result = new List<IWopiFolder>();
        if (resourceType == WopiResourceType.File)
        {
            identifier = EncodeIdentifier(GetFileParentIdentifier(identifier));
        }
        var container = GetWopiContainer(identifier)
            ?? throw new DirectoryNotFoundException($"Container with identifier '{identifier}' not found.");
        if (container.Identifier == RootContainerPointer.Identifier && resourceType == WopiResourceType.File)
        {
            result.Add(container);
        }

        while (container.Identifier != RootContainerPointer.Identifier)
        {
            var parent = GetFolderParentIdentifier(container.Identifier);
            container = GetWopiContainer(EncodeIdentifier(parent));
            result.Add(container);
        }
        //result.Add(RootContainerPointer);
        result.Reverse();
        return Task.FromResult(result.AsReadOnly());
    }

    /// <inheritdoc/>
    public Task<IWopiResource?> GetWopiResourceByName(
        WopiResourceType resourceType, 
        string containerId, 
        string name, 
        CancellationToken cancellationToken = default)
    {
        var fullPath = DecodeFullPath(containerId);
        var namePath = Path.Combine(fullPath, name);
        var newId = Path.GetRelativePath(WopiAbsolutePath, namePath);
        if (resourceType == WopiResourceType.File && !newId.StartsWith(rootPath))
        {
            newId = rootPath + newId;
        }
        IWopiResource? result = resourceType switch
        {
            WopiResourceType.File => File.Exists(namePath)
                ? GetWopiFile(EncodeIdentifier(newId))
                : null,
            WopiResourceType.Container => Directory.Exists(namePath)
                ? GetWopiContainer(EncodeIdentifier(newId))
                : null,
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
        return Task.FromResult(result);
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
        var fullPath = DecodeFullPath(containerId);
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
            WopiResourceType.File => await CreateWopiFile(containerId ?? rootPath, name),
            WopiResourceType.Container => await CreateWopiChildContainer(containerId ?? rootPath, name),
            _ => throw new NotSupportedException("Unsupported resource type.")
        };
    }

    private async Task<IWopiFile> CreateWopiFile(
        string containerId,
        string name)
    {
        var fullPath = DecodeFullPath(containerId);
        var newPath = Path.Combine(fullPath, name);
        if (File.Exists(newPath))
        {
            throw new ArgumentException("File already exists.", nameof(name));
        }

        // Create an empty file
        using (var fs = new FileStream(newPath, FileMode.CreateNew))
        {
            // Create a 0-byte file
        }

        var newFileId = new DirectoryInfo(fullPath).FullName == WopiRootPath
            ? Path.Combine(rootPath, name)
            : rootPath + Path.GetRelativePath(WopiAbsolutePath, newPath).TrimStart('.');
        return await Task.FromResult(
            GetWopiFile(EncodeIdentifier(newFileId)));
    }

    private Task<IWopiFolder> CreateWopiChildContainer(
        string containerId,
        string name)
    {
        var fullPath = DecodeFullPath(containerId);

        var newPath = Path.Combine(fullPath, name);
        var dirInfo = new DirectoryInfo(newPath);
        if (dirInfo.Exists)
        {
            throw new ArgumentException("Directory already exists.", nameof(name));
        }
        else
        {
            dirInfo.Create();
        }
        return Task.FromResult(
            GetWopiContainer(EncodeIdentifier(Path.GetRelativePath(WopiAbsolutePath, dirInfo.FullName))));
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
        var fullPath = DecodeFullPath(identifier);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException("Directory not found");
        }
        if (Directory.EnumerateFileSystemEntries(fullPath).Any())
        {
            throw new InvalidOperationException("Directory is not empty.");
        }
        Directory.Delete(fullPath, true);
        return true;
    }

    private bool DeleteWopiFile(string identifier)
    {
        var fullPath = DecodeFullPath(identifier);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("File not found");
        }
        File.Delete(fullPath);
        return true;
    }

    /// <inheritdoc/>
    public Task<bool> RenameWopiResource(WopiResourceType resourceType, string identifier, string requestedName, CancellationToken cancellationToken = default)
    {
        if (resourceType != WopiResourceType.Container)
        {
            throw new NotSupportedException("Only containers can be renamed.");
        }
        if (requestedName.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new ArgumentException(message: "Invalid characters in the name.", paramName: nameof(requestedName));
        }
        var fullPath = DecodeFullPath(identifier);
        var parentPath = (new DirectoryInfo(fullPath).Parent?.FullName) 
            ?? throw new DirectoryNotFoundException("Directory not found");
        var newPath = Path.Combine(parentPath, requestedName);
        if (Directory.Exists(newPath))
        {
            throw new InvalidOperationException("Directory already exists.");
        }
        Directory.Move(fullPath, newPath);
        return Task.FromResult(true);
    }
    #endregion

    private static string DecodeIdentifier(string identifier)
    {
        var bytes = Convert.FromBase64String(identifier);
        return Encoding.UTF8.GetString(bytes);
    }

    private static string EncodeIdentifier(string path)
    {
        var bytes = Encoding.UTF8.GetBytes(path);
        return Convert.ToBase64String(bytes);
    }

    private string GetFileParentIdentifier(string identifier)
    {
        var fullPath = DecodeFullPath(identifier);
        var dirName = new FileInfo(fullPath).Directory?.FullName;
        return dirName is null || dirName == WopiAbsolutePath
            ? rootPath
            : rootPath + Path.GetRelativePath(WopiAbsolutePath, dirName);
    }

    private string GetFolderParentIdentifier(string identifier)
    {
        var fullPath = DecodeFullPath(identifier);
        var dirInfo = new DirectoryInfo(fullPath);
        return dirInfo.FullName == WopiAbsolutePath
            ? rootPath
            : rootPath + Path.GetRelativePath(WopiAbsolutePath, dirInfo.Parent!.FullName).TrimStart('.');
    }

    private string DecodeFullPath(string identifier)
    {
        var folderPath = DecodeIdentifier(identifier);
        return Path.Combine(WopiAbsolutePath, folderPath);
    }
}