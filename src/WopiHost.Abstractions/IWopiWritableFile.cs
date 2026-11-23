namespace WopiHost.Abstractions;

/// <summary>
/// Represents the writable view of a WOPI file: the read members of <see cref="IWopiFile"/>
/// plus <see cref="OpenWriteAsync"/>.
/// </summary>
/// <remarks>
/// Returned only by <see cref="IWopiWritableStorageProvider.CreateWopiChildFile"/> and
/// <see cref="IWopiWritableStorageProvider.GetWritableFile"/>; the read-side
/// <see cref="IWopiStorageProvider.GetWopiFile"/> returns the narrower <see cref="IWopiFile"/>,
/// so the compiler keeps read-only flows from writing.
/// </remarks>
public interface IWopiWritableFile : IWopiFile
{
    /// <summary>
    /// Opens the file for writing.
    /// </summary>
    /// <remarks>
    /// Ownership of the returned <see cref="Stream"/> transfers to the caller, who must dispose
    /// it, typically via <c>await using var stream = await file.OpenWriteAsync(ct);</c>. On most
    /// providers, disposing the stream is what commits the write.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A writable <see cref="Stream"/> owned by the caller.</returns>
    Task<Stream> OpenWriteAsync(CancellationToken cancellationToken = default);
}
