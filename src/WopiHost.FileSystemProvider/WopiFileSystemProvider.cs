using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Provides files and folders based on a base64-encoded paths.
/// </summary>
public class WopiFileSystemProvider : IWopiStorageProvider
{
    private WopiFileSystemProviderOptions FileSystemProviderOptions { get; }

    private const string _rootPath = @".\";

    private string WopiRootPath => FileSystemProviderOptions.RootPath;

    private string WopiAbsolutePath => Path.IsPathRooted(WopiRootPath) ? WopiRootPath : Path.Combine(HostEnvironment.ContentRootPath, WopiRootPath);

    /// <summary>
    /// Reference to the root container.
    /// </summary>
    public IWopiFolder RootContainerPointer => new WopiFolder(_rootPath, EncodeIdentifier(_rootPath));

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
        var filePath = DecodeIdentifier(identifier);
        return new WopiFile(Path.Combine(WopiAbsolutePath, filePath), identifier);
    }

    /// <inheritdoc/>
    public IWopiFolder GetWopiContainer(string identifier = "")
    {
        var folderPath = DecodeIdentifier(identifier);
        return new WopiFolder(Path.Combine(WopiAbsolutePath, folderPath), identifier);
    }

    /// <inheritdoc/>
    public List<IWopiFile> GetWopiFiles(string identifier = "")
    {
        var folderPath = DecodeIdentifier(identifier);
        var files = new List<IWopiFile>();
        foreach (var path in Directory.GetFiles(Path.Combine(WopiAbsolutePath, folderPath)))  //TODO Directory.Enumerate...
        {
            var filePath = Path.Combine(folderPath, Path.GetFileName(path));
            var fileId = EncodeIdentifier(filePath);
            files.Add(GetWopiFile(fileId));
        }
        return files;
    }

    /// <inheritdoc/>
    public List<IWopiFolder> GetWopiContainers(string identifier = "")
    {
        var folderPath = DecodeIdentifier(identifier);
        var folders = new List<IWopiFolder>();
        foreach (var directory in Directory.GetDirectories(Path.Combine(WopiAbsolutePath, folderPath)))
        {
            var subfolderPath = "." + directory.Remove(0, directory.LastIndexOf(Path.DirectorySeparatorChar));
            var folderId = EncodeIdentifier(subfolderPath);
            folders.Add(GetWopiContainer(folderId));
        }
        return folders;
    }

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
}