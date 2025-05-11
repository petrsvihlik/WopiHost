using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Principal;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <inheritdoc/>
/// <summary>
/// Creates an instance of <see cref="WopiFile"/>.
/// </summary>
/// <param name="filePath">Path on the file system the file is located in.</param>
/// <param name="fileIdentifier">Identifier of a file.</param>
public class WopiFile(string filePath, string fileIdentifier) : IWopiFile
{
    private readonly FileInfo fileInfo = new(filePath);
    private readonly FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(filePath);

    /// <inheritdoc/>
    public string Identifier { get; } = fileIdentifier;

    /// <inheritdoc />
    public bool Exists => fileInfo.Exists;

    /// <inheritdoc/>
    public string Extension => fileInfo.Extension.TrimStart('.');

    /// <inheritdoc/>
    public string? Version => fileVersionInfo.FileVersion ?? fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    /// <inheritdoc/>
#pragma warning disable CA1819 // Properties should not return arrays
    public byte[]? Checksum { get; } = null;
#pragma warning restore CA1819 // Properties should not return arrays

    /// <inheritdoc/>
    public long Size => fileInfo.Length;

    /// <inheritdoc/>
    public long Length => fileInfo.Length;

    /// <inheritdoc/>
    public string Name => Path.GetFileNameWithoutExtension(fileInfo.Name);

    /// <inheritdoc/>
    public DateTime LastWriteTimeUtc => fileInfo.LastWriteTimeUtc;

    /// <inheritdoc/>
    public Task<Stream> GetReadStream(CancellationToken cancellationToken = default) => Task.FromResult<Stream>(fileInfo.OpenRead());

    /// <inheritdoc/>
    public Task<Stream> GetWriteStream(CancellationToken cancellationToken = default) => Task.FromResult<Stream>(fileInfo.Open(FileMode.Truncate));

    /// <summary>
    /// A string that uniquely identifies the owner of the file.
    /// Supported only on Windows and Linux.
    /// https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1416
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
