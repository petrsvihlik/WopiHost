namespace WopiHost.Abstractions;

/// <summary>
/// Read-side view of a WOPI file. Fetched from <see cref="IWopiStorageProvider.GetWopiFile"/>
/// when the caller only needs to enumerate metadata or stream content out.
/// </summary>
/// <remarks>
/// The write seam is split off into <see cref="IWopiWritableFile"/> so a
/// read-only flow can't accidentally call <c>OpenWriteAsync</c> on a file it fetched read-only.
/// Callers that need to mutate file content fetch via
/// <see cref="IWopiWritableStorageProvider.GetWritableFile"/> or take the writable file directly
/// from <see cref="IWopiWritableStorageProvider.CreateWopiChildFile"/>.
/// </remarks>
public interface IWopiFile : IWopiResource
{
    /// <summary>
    /// A non-null string that uniquely identifies the owner of the file (for example, a user id,
    /// principal name, or login name — the meaning is provider-defined). Surfaces on the wire as
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
    /// Indicates whether the file already exists.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// Size of the file in bytes. Matches the <c>Length</c> property on .NET filesystem types
    /// (<see cref="System.IO.FileInfo.Length"/>, <see cref="System.IO.Stream.Length"/>) and feeds
    /// the WOPI <c>CheckFileInfo.Size</c> response field on the wire.
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
    /// SHA-256 checksum of the file contents, or <see langword="null"/> when the provider doesn't
    /// compute one. <see cref="ReadOnlyMemory{T}"/> hands callers an immutable view; providers
    /// can wrap their existing <c>byte[]</c> hash output via the implicit conversion from
    /// <c>byte[]</c> to <see cref="ReadOnlyMemory{T}"/>.
    /// </summary>
    ReadOnlyMemory<byte>? Checksum { get; }

    /// <summary>
    /// Opens the file for reading. Ownership of the returned <see cref="Stream"/> is transferred
    /// to the caller, who must dispose it when done — typically via
    /// <c>await using var stream = await file.OpenReadAsync(ct);</c>.
    /// </summary>
    /// <remarks>
    /// Naming mirrors the .NET BCL convention for resource-acquiring openers
    /// (<see cref="System.IO.File.OpenRead(string)"/>, <c>BlobClient.OpenReadAsync</c>,
    /// <c>HttpClient.GetStreamAsync</c>): <c>Open*</c> signals that the call acquires a resource
    /// the caller must release, in contrast to a property-like <c>Get*</c> which would not.
    /// <see cref="Stream"/> already implements <see cref="IAsyncDisposable"/> (since .NET Core
    /// 3.0), so <c>await using</c> is the idiomatic disposal path.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-positioned <see cref="Stream"/> owned by the caller.</returns>
    Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);
}
