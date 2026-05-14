namespace WopiHost.Abstractions;

/// <summary>
/// Implementation of writable operations for an external storage provider.
/// </summary>
/// <remarks>
/// Per #420 item 1.1, the file and container mutation methods are exposed as distinct typed
/// pairs rather than a single generic <c>&lt;T&gt;</c> that used <c>typeof(T)</c> as a runtime
/// discriminator. File-name and container-name validation rules differ (e.g. extension handling,
/// allowed characters), so the split also makes the semantic mismatch visible in the API surface.
/// </remarks>
public interface IWopiWritableStorageProvider
{
    /// <summary>
    /// An integer value that indicates the maximum length for file names that the WOPI host supports, excluding the file extension.
    /// The default value is 250. Note that WOPI clients will use this default value if the property is omitted or if it is explicitly set to <c>0</c>.
    /// </summary>
    int FileNameMaxLength { get; }

    /// <summary>
    /// Creates a new 0-byte file under the specified container. Returned as
    /// <see cref="IWopiWritableFile"/> so the caller can immediately stream content into it.
    /// </summary>
    /// <param name="containerId">
    /// Identifier of the parent container. Required. Pass
    /// <c><see cref="IWopiStorageProvider.RootContainer"/>.Identifier</c> to create directly under
    /// the root.
    /// </param>
    /// <param name="name">The new file's name (including extension).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IWopiWritableFile?> CreateWopiChildFile(
        string containerId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a writable handle to the file identified by <paramref name="identifier"/>, or
    /// <see langword="null"/> if no file with that id exists. The writable interface is the
    /// gate for mutating an existing file's contents — read-only consumers should call
    /// <see cref="IWopiStorageProvider.GetWopiFile"/> instead.
    /// </summary>
    /// <remarks>
    /// Resolves the #420 item 1.2 leak: pre-fix the read-side <c>GetWopiFile</c> returned a
    /// file that also exposed <c>OpenWriteAsync</c>, letting any caller mutate a file
    /// they'd fetched in a read-only flow. After the split, writes require a deliberate
    /// fetch through the writable storage provider.
    /// </remarks>
    /// <param name="identifier">File identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IWopiWritableFile?> GetWritableFile(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new empty child container under the specified container.
    /// </summary>
    /// <param name="containerId">Identifier of the parent container. Required.</param>
    /// <param name="name">The new container's name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IWopiContainer?> CreateWopiChildContainer(
        string containerId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contract for the missing/exception split (clarified in #380 item 4.2 so the two in-tree
    /// providers and any future impl behave the same):
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// Returns <see langword="false"/> when <paramref name="identifier"/> does not resolve to a
    /// file. Controllers map this to <c>404 Not Found</c> per the WOPI spec.
    /// </item>
    /// <item>
    /// <see cref="WopiResourceLockedException"/> surfaces through the lock-aware decorator when
    /// the file is currently WOPI-locked; controllers translate to <c>409 Conflict</c> with
    /// the X-WOPI-Lock response header.
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="identifier">File identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the file existed and was deleted; <see langword="false"/> if it did not exist.</returns>
    Task<bool> DeleteWopiFile(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns <see langword="false"/> when <paramref name="identifier"/> does not resolve to a
    /// container (→ 404). Throws <see cref="InvalidOperationException"/> when the container
    /// exists but cannot be deleted (e.g. still has children) → controllers translate to
    /// <c>409 Conflict</c>.
    /// </para>
    /// </remarks>
    /// <param name="identifier">Container identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the container existed and was deleted; <see langword="false"/> if it did not exist.</returns>
    Task<bool> DeleteWopiContainer(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames an existing file.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="false"/> when <paramref name="identifier"/> does not resolve to a
    /// file (→ 404). Throws <see cref="ArgumentException"/> for bad names and
    /// <see cref="InvalidOperationException"/> when the target name already exists
    /// (→ 409 with <c>X-WOPI-InvalidFileNameError</c>).
    /// </remarks>
    /// <param name="identifier">File identifier.</param>
    /// <param name="requestedName">The new file name (including extension).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> RenameWopiFile(string identifier, string requestedName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames an existing container.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="false"/> when <paramref name="identifier"/> does not resolve to a
    /// container (→ 404). Throws <see cref="ArgumentException"/> for bad names and
    /// <see cref="InvalidOperationException"/> when the target name already exists (→ 409).
    /// </remarks>
    /// <param name="identifier">Container identifier.</param>
    /// <param name="requestedName">The new container name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> RenameWopiContainer(string identifier, string requestedName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether <paramref name="name"/> is acceptable as a file name (allowed characters,
    /// length up to <see cref="FileNameMaxLength"/>, no path separators).
    /// </summary>
    /// <param name="name">Candidate file name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> CheckValidFileName(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether <paramref name="name"/> is acceptable as a container name. Validation
    /// rules differ from file names (e.g. extension constraints don't apply).
    /// </summary>
    /// <param name="name">Candidate container name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> CheckValidContainerName(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <paramref name="name"/> unchanged if no file with that name exists under
    /// <paramref name="containerId"/>; otherwise returns a deduplicated variant (typically
    /// <c>"name (1).ext"</c>, <c>"name (2).ext"</c>, …).
    /// </summary>
    /// <param name="containerId">Parent container id.</param>
    /// <param name="name">Candidate file name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string> GetSuggestedFileName(string containerId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <paramref name="name"/> unchanged if no container with that name exists under
    /// <paramref name="containerId"/>; otherwise returns a deduplicated variant.
    /// </summary>
    /// <param name="containerId">Parent container id.</param>
    /// <param name="name">Candidate container name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string> GetSuggestedContainerName(string containerId, string name, CancellationToken cancellationToken = default);
}
