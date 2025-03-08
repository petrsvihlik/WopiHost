using System.Collections.ObjectModel;
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
    public ReadOnlyCollection<IWopiFile> GetWopiFiles(string identifier = "")
    {
        var folderPath = DecodeIdentifier(identifier);
        var files = new List<IWopiFile>();
        foreach (var path in Directory.GetFiles(Path.Combine(WopiAbsolutePath, folderPath)))
        {
            var filePath = Path.Combine(folderPath, Path.GetFileName(path));
            var fileId = EncodeIdentifier(filePath);
            files.Add(GetWopiFile(fileId));
        }
        return files.AsReadOnly();
    }

    /// <inheritdoc/>
    public ReadOnlyCollection<IWopiFolder> GetWopiContainers(string identifier = "")
    {
        var folderPath = DecodeIdentifier(identifier);
        var folders = new List<IWopiFolder>();
        foreach (var directory in Directory.GetDirectories(Path.Combine(WopiAbsolutePath, folderPath)))
        {
            //var subfolderPath = "." + directory.Remove(0, directory.LastIndexOf(Path.DirectorySeparatorChar));
            var subfolderPath = Path.GetRelativePath(WopiAbsolutePath, directory);
            var folderId = EncodeIdentifier(subfolderPath);
            folders.Add(GetWopiContainer(folderId));
        }
        return folders.AsReadOnly();
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

    #region IWopiWritableStorageProvider

    /// <inheritdoc/>
    public Task<string?> CreateWopiChildContainer(
        string identifier,
        string name,
        bool isExactName,
        CancellationToken cancellationToken = default)
    {
        var fullPath = DecodeFullPath(identifier);

        var newPath = Path.Combine(fullPath, name);
        if (Directory.Exists(newPath))
        {
            if (isExactName)
            {
                return Task.FromResult<string?>(null);
            }
            else
            {
                var newName = name;
                var counter = 1;
                while (Directory.Exists(Path.Combine(fullPath, newName)))
                {
                    newName = $"{name} ({counter++})";
                }
                newPath = Path.Combine(fullPath, newName);
            }
        }
        var dirInfo = Directory.CreateDirectory(newPath);
        return Task.FromResult<string?>(
            EncodeIdentifier(
                Path.GetRelativePath(WopiAbsolutePath, dirInfo.FullName)));
    }

    /// <inheritdoc/>
    public Task<bool> DeleteWopiContainer(string identifier, CancellationToken cancellationToken = default)
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