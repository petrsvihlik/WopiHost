namespace WopiHost.Abstractions;

/// <summary>
/// Represents the read-side view of a WOPI file, as returned by
/// <see cref="IWopiStorageProvider.GetWopiFile"/> for reading metadata or streaming content out.
/// </summary>
/// <remarks>
/// The write seam lives on <see cref="IWopiWritableFile"/> so a read-only flow can't call
/// <see cref="IWopiWritableFile.OpenWriteAsync"/> on a file it fetched read-only.
/// Callers that need to mutate file content fetch via
/// <see cref="IWopiWritableStorageProvider.GetWritableFile"/> or take the writable file directly
/// from <see cref="IWopiWritableStorageProvider.CreateWopiChildFile"/>.
/// </remarks>
public interface IWopiFile : IWopiResource
{
    /// <summary>
    /// Gets a string that uniquely identifies the owner of the file (for example, a user id,
    /// principal name, or login name; the meaning is provider-defined). Surfaces on the wire as
    /// <c>CheckFileInfo.OwnerId</c>.
    /// </summary>
    /// <remarks>
    /// This is best-effort metadata: implementations return <see cref="string.Empty"/> when the
    /// owner can't be determined — the provider doesn't track ownership, the underlying store
    /// doesn't expose it, the host platform doesn't support the lookup, or reading it failed.
    /// Implementations MUST NOT return <see langword="null"/> and MUST NOT throw; ownership is
    /// non-essential to a <c>CheckFileInfo</c> response, so a failed lookup degrades to empty rather
    /// than failing the request.
    /// </remarks>
    string Owner { get; }

    /// <summary>
    /// Gets a value indicating whether the file exists.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// Gets the size of the file in bytes. Surfaces on the wire as <c>CheckFileInfo.Size</c>.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Gets the time, in UTC, of the last modification of the file.
    /// </summary>
    DateTime LastWriteTimeUtc { get; }

    /// <summary>
    /// Gets the file extension without the leading dot.
    /// </summary>
    string Extension { get; }

    /// <summary>
    /// Gets the current version of the file, or <see langword="null"/> if the provider doesn't
    /// track one.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, <c>CheckFileInfo.Version</c> falls back to
    /// <see cref="LastWriteTimeUtc"/>.
    /// </remarks>
    string? Version { get; }

    /// <summary>
    /// Gets the SHA-256 checksum of the file contents, or <see langword="null"/> when the
    /// provider doesn't compute one.
    /// </summary>
    ReadOnlyMemory<byte>? Checksum { get; }

    /// <summary>
    /// Opens the file for reading.
    /// </summary>
    /// <remarks>
    /// Ownership of the returned <see cref="Stream"/> transfers to the caller, who must dispose
    /// it, typically via <c>await using var stream = await file.OpenReadAsync(ct);</c>.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A readable <see cref="Stream"/> positioned at the start of the content and owned
    /// by the caller.</returns>
    Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);
}
