using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Principal;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <inheritdoc/>
public class WopiFile : IWopiFile
{
    private readonly FileInfo fileInfo;
    private readonly FileVersionInfo fileVersionInfo;

    /// <inheritdoc/>
    public string Identifier { get; }

    /// <inheritdoc />
    public bool Exists => fileInfo.Exists;

    /// <inheritdoc/>
    public string Extension
    {
        get
        {
            var ext = fileInfo.Extension;
            if (ext.StartsWith('.'))
            {
                ext = ext[1..];
            }
            return ext;
        }
    }

    /// <inheritdoc/>
    public string Version => fileVersionInfo.FileVersion ?? fileInfo.LastWriteTimeUtc.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public long Size => fileInfo.Length;

    /// <inheritdoc/>
    public long Length => fileInfo.Length;

    /// <inheritdoc/>
    public string Name => fileInfo.Name;

    /// <inheritdoc/>
    public DateTime LastWriteTimeUtc => fileInfo.LastWriteTimeUtc;

    /// <inheritdoc/>
    public Stream GetReadStream() => fileInfo.OpenRead();

    /// <inheritdoc/>
    public Stream GetWriteStream() => fileInfo.Open(FileMode.Truncate);

    /// <summary>
    /// Creates an instance of <see cref="WopiFile"/>.
    /// </summary>
    /// <param name="filePath">Path on the file system the file is located in.</param>
    /// <param name="fileIdentifier">Identifier of a file.</param>
    public WopiFile(string filePath, string fileIdentifier)
    {
        fileInfo = new FileInfo(filePath);
        fileVersionInfo = FileVersionInfo.GetVersionInfo(filePath);
        Identifier = fileIdentifier;
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
                return fileInfo.GetAccessControl().GetOwner(typeof(NTAccount))?.ToString() ?? string.Empty;
            }
            //else if (OperatingSystem.IsLinux())
            //{
            //    return Mono.Unix.UnixFileSystemInfo.GetFileSystemEntry(FilePath).OwnerUser.UserName; //TODO: test
            //}
            else
            {
                return "UNSUPPORTED_PLATFORM";
            }
        }
    }
}
