namespace WopiHost.Abstractions;

/// <summary>
/// Writable view of a WOPI file. Carries the read members from <see cref="IWopiFile"/> and
/// adds the write seam (<see cref="OpenWriteAsync"/>). Returned only by
/// <see cref="IWopiWritableStorageProvider.CreateWopiChildFile"/> and
/// <see cref="IWopiWritableStorageProvider.GetWritableFile"/>; the read-side
/// <see cref="IWopiStorageProvider.GetWopiFile"/> returns the narrower <see cref="IWopiFile"/>
/// so a read-only flow can't accidentally write through a file it fetched read-only.
/// </summary>
/// <remarks>
/// Resolves #420 item 1.2 — pre-fix, <see cref="OpenWriteAsync"/> lived on <see cref="IWopiFile"/>
/// itself, so any caller with a file handle could write to it regardless of how the handle was
/// obtained. The split makes the write capability part of the type the storage layer hands out
/// for writable flows, and the compiler enforces the gate.
/// </remarks>
public interface IWopiWritableFile : IWopiFile
{
    /// <summary>
    /// Opens the file for writing. Ownership of the returned <see cref="Stream"/> is transferred
    /// to the caller, who must dispose it when done — typically via
    /// <c>await using var stream = await file.OpenWriteAsync(ct);</c>. Disposing the stream is
    /// what commits the write on most providers.
    /// </summary>
    /// <remarks>
    /// See <see cref="IWopiFile.OpenReadAsync"/> for the naming/ownership rationale.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A writable <see cref="Stream"/> owned by the caller.</returns>
    Task<Stream> OpenWriteAsync(CancellationToken cancellationToken = default);
}
