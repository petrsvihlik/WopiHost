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
    private readonly FileInfo _fileInfo = new(filePath);
    private readonly FileVersionInfo _fileVersionInfo = FileVersionInfo.GetVersionInfo(filePath);

    /// <inheritdoc/>
    public string Identifier { get; } = fileIdentifier;

    /// <inheritdoc />
    public bool Exists => _fileInfo.Exists;

    /// <inheritdoc/>
    public string Extension => _fileInfo.Extension.TrimStart('.');

    /// <inheritdoc/>
    public string? Version => _fileVersionInfo.FileVersion ?? _fileInfo.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public ReadOnlyMemory<byte>? Checksum => null;

    /// <inheritdoc/>
    public long Length => _fileInfo.Length;

    /// <inheritdoc/>
    public string Name => Path.GetFileNameWithoutExtension(_fileInfo.Name);

    /// <inheritdoc/>
    public DateTime LastWriteTimeUtc => _fileInfo.LastWriteTimeUtc;

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) => Task.FromResult<Stream>(_fileInfo.OpenRead());

    /// <inheritdoc/>
    public Task<Stream> OpenWriteAsync(CancellationToken cancellationToken = default) => Task.FromResult<Stream>(_fileInfo.Open(FileMode.Truncate));

    /// <summary>
    /// A string that uniquely identifies the owner of the file.
    /// Supported only on Windows and Linux. Throws
    /// <see cref="PlatformNotSupportedException"/> on other platforms.
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
                return _fileInfo.GetAccessControl().GetOwner(typeof(NTAccount))?.ToString() ?? string.Empty;
            }
            if (OperatingSystem.IsLinux())
            {
                return LinuxFileOwner.GetOwnerName(_fileInfo.FullName);
            }
            throw new PlatformNotSupportedException(
                "WopiFile.Owner is only supported on Windows and Linux.");
        }
    }
}
