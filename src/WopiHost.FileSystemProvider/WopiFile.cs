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

    // FileVersionInfo.GetVersionInfo throws FileNotFoundException for a missing path, yet a
    // WopiFile may legitimately represent one — it exposes Exists, and an id→path map entry can
    // outlive a rename or delete (the map is rebuilt only at startup). Read it lazily and guard on
    // existence so constructing the file never faults deep inside CheckFileInfo; a stale id then
    // surfaces as Exists=false instead of a 500.
    private readonly Lazy<FileVersionInfo?> _fileVersionInfo =
        new(() => File.Exists(filePath) ? FileVersionInfo.GetVersionInfo(filePath) : null);

    /// <inheritdoc/>
    public string Identifier { get; } = fileIdentifier;

    /// <inheritdoc />
    public bool Exists => _fileInfo.Exists;

    /// <inheritdoc/>
    public string Extension => _fileInfo.Extension.TrimStart('.');

    /// <inheritdoc/>
    public string? Version => _fileVersionInfo.Value?.FileVersion ?? _fileInfo.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);

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
    /// Resolves the OS-level owning user on Windows (ACL owner as an <see cref="NTAccount"/>),
    /// Linux (file UID → name via <see cref="LinuxFileOwner"/>) and macOS (file UID → name via
    /// <see cref="MacFileOwner"/>). Any other platform has no ownership lookup wired up and returns
    /// <see cref="string.Empty"/>. Per the <see cref="IWopiFile.Owner"/> contract this getter is
    /// best-effort and never throws: a failed or unsupported lookup degrades to empty rather than
    /// faulting <c>CheckFileInfo</c>.
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
                if (OperatingSystem.IsMacOS())
                {
                    return MacFileOwner.GetOwnerName(_fileInfo.FullName);
                }
                return string.Empty;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The file vanished, access was denied, or the native lookup failed. Ownership is
                // non-essential metadata, so the contract degrades to empty rather than throwing.
                return string.Empty;
            }
        }
    }
}
