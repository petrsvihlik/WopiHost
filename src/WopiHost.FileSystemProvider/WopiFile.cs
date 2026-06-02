using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <inheritdoc/>
/// <summary>
/// Creates an instance of <see cref="WopiFile"/>.
/// </summary>
/// <param name="filePath">Path on the file system the file is located in.</param>
/// <param name="fileIdentifier">Identifier of a file.</param>
public class WopiFile(string filePath, string fileIdentifier) : IWopiWritableFile
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
    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Stream>(_fileInfo.OpenRead());
    }

    /// <inheritdoc/>
    public Task<Stream> OpenWriteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Stream>(_fileInfo.Open(FileMode.Truncate));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Resolves the OS-level owning user on Windows (ACL owner as an <see cref="NTAccount"/>) and
    /// Linux (file UID → name via <see cref="LinuxFileOwner"/>). macOS and any other platform have
    /// no ownership lookup wired up, so they return <see cref="string.Empty"/>. Per the
    /// <see cref="IWopiFile.Owner"/> contract this getter is best-effort and never throws: a failed
    /// or unsupported lookup degrades to empty rather than faulting <c>CheckFileInfo</c>.
    /// </remarks>
    public string Owner
    {
        get
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    return _fileInfo.GetAccessControl().GetOwner(typeof(NTAccount))?.ToString() ?? string.Empty;
                }
                if (OperatingSystem.IsLinux())
                {
                    return LinuxFileOwner.GetOwnerName(_fileInfo.FullName);
                }
                // macOS / other platforms: no ownership lookup is implemented.
                return string.Empty;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The file vanished, access was denied, or the native lookup failed. Ownership is
                // non-essential metadata, so honour the contract and degrade to empty.
                return string.Empty;
            }
        }
    }
}
