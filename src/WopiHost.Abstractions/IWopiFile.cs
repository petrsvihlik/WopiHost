namespace WopiHost.Abstractions;

/// <summary>
/// Representation of a file.
/// </summary>
public interface IWopiFile : IWopiResource
{
    /// <summary>
    /// A string that uniquely identifies the owner of the file.
    /// </summary>
    string Owner { get; }

    /// <summary>
    /// Indicates whether the file already exists.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// Gets size of the file in bytes.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Time of the last modification of the file.
    /// </summary>
    DateTime LastWriteTimeUtc { get; }

    /// <summary>
    /// Extension without the initial dot.
    /// </summary>
    string Extension { get; }

    /// <summary>
    /// Version of the file.
    /// </summary>
    string? Version { get; }

    /// <summary>
    /// SHA256 checksum
    /// </summary>
    byte[]? Checksum { get; }

    /// <summary>
    /// Size of the file.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Gets read-only stream.
    /// </summary>
    Task<Stream> GetReadStream(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets r/w stream.
    /// </summary>
    Task<Stream> GetWriteStream(CancellationToken cancellationToken = default);
}
