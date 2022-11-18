using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Principal;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <inheritdoc/>
public class WopiFile : IWopiFile
{
    private FileInfo _fileInfo;

    private FileVersionInfo _fileVersionInfo;

    private string FilePath { get; set; }

    private FileInfo FileInfo => _fileInfo ??= new FileInfo(FilePath);

    private FileVersionInfo FileVersionInfo => _fileVersionInfo ??= FileVersionInfo.GetVersionInfo(FilePath);

    /// <inheritdoc/>
    public string Identifier { get; }

    /// <inheritdoc />
    public bool Exists => FileInfo.Exists;

    /// <inheritdoc/>
    public string Extension
    {
        get
        {
            var ext = FileInfo.Extension;
            if (ext.StartsWith(".", StringComparison.InvariantCulture))
            {
                ext = ext[1..];
            }
            return ext;
        }
    }

    /// <inheritdoc/>
    public string Version => FileVersionInfo.FileVersion ?? FileInfo.LastWriteTimeUtc.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public long Size => FileInfo.Length;

    /// <inheritdoc/>
    public long Length => FileInfo.Length;

    /// <inheritdoc/>
    public string Name => FileInfo.Name;

    /// <inheritdoc/>
    public DateTime LastWriteTimeUtc => FileInfo.LastWriteTimeUtc;

    /// <summary>
    /// Creates an instance of <see cref="WopiFile"/>.
    /// </summary>
    /// <param name="filePath">Path on the file system the file is located in.</param>
    /// <param name="fileIdentifier">Identifier of a file.</param>
    public WopiFile(string filePath, string fileIdentifier)
    {
        FilePath = filePath;
        Identifier = fileIdentifier;
    }

    /// <inheritdoc/>
    public Stream GetReadStream()
    {
        return FileInfo.OpenRead();
    }

    /// <inheritdoc/>
    public Stream GetWriteStream()
    {
        return FileInfo.Open(FileMode.Truncate);
    }

    /// <summary>
    /// A string that uniquely identifies the owner of the file.
    /// Supported only on Windows and Linux.
    /// https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1416
    /// </summary>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("windows")]
    public string Owner
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return FileInfo.GetAccessControl().GetOwner(typeof(NTAccount)).ToString();
            }
            else if (OperatingSystem.IsLinux())
            {
                return Mono.Unix.UnixFileSystemInfo.GetFileSystemEntry(FilePath).OwnerUser.UserName; //TODO: test
            }
            else
            {
                return "UNSUPPORTED_PLATFORM";
            }
        }
    }
}
