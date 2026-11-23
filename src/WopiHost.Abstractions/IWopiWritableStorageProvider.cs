namespace WopiHost.Abstractions;

/// <summary>
/// Provides write operations on files and containers in a storage backend.
/// </summary>
/// <remarks>
/// File and container mutations are separate typed methods because their name-validation
/// rules can differ (for example extension handling or allowed characters).
/// </remarks>
public interface IWopiWritableStorageProvider
{
    /// <summary>
    /// Gets the maximum file-name length the host supports, excluding the file extension.
    /// </summary>
    /// <remarks>
    /// Reported as <c>CheckFileInfo.FileNameMaxLength</c>. WOPI clients assume <c>250</c> when the
    /// value is omitted or <c>0</c>.
    /// </remarks>
    int FileNameMaxLength { get; }

    /// <summary>
    /// Creates a new empty file in the specified container.
    /// </summary>
    /// <param name="containerId">
    /// The identifier of the parent container. Pass
    /// <c><see cref="IWopiStorageProvider.RootContainer"/>.Identifier</c> to create directly in
    /// the root.
    /// </param>
    /// <param name="name">The new file's name, including the extension.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created file, writable so the caller can stream content into it.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid file name, or a
    /// file with that name already exists.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="containerId"/> does not
    /// resolve to a container.</exception>
    Task<IWopiWritableFile?> CreateWopiChildFile(
        string containerId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a writable handle to the file identified by <paramref name="identifier"/>.
    /// </summary>
    /// <remarks>
    /// This is the only way to obtain a writable handle to an existing file; read-only consumers
    /// should call <see cref="IWopiStorageProvider.GetWopiFile"/> instead.
    /// </remarks>
    /// <param name="identifier">The identifier of the file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The writable file, or <see langword="null"/> if no file with that id exists.</returns>
    Task<IWopiWritableFile?> GetWritableFile(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new empty child container in the specified container.
    /// </summary>
    /// <param name="containerId">The identifier of the parent container.</param>
    /// <param name="name">The new container's name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created container.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid container name,
    /// or a container with that name already exists.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="containerId"/> does not
    /// resolve to a container.</exception>
    Task<IWopiContainer?> CreateWopiChildContainer(
        string containerId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified file.
    /// </summary>
    /// <remarks>
    /// The endpoints map a <see langword="false"/> result to <c>404 Not Found</c>.
    /// </remarks>
    /// <param name="identifier">The identifier of the file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the file existed and was deleted; <see langword="false"/> if it did not exist.</returns>
    /// <exception cref="WopiResourceLockedException">The file is WOPI-locked. Thrown by the
    /// lock-aware storage decorator when registered; the endpoints translate it to
    /// <c>409 Conflict</c> with the <c>X-WOPI-Lock</c> response header.</exception>
    Task<bool> DeleteWopiFile(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified container.
    /// </summary>
    /// <remarks>
    /// The endpoints map a <see langword="false"/> result to <c>404 Not Found</c>.
    /// </remarks>
    /// <param name="identifier">The identifier of the container.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the container existed and was deleted; <see langword="false"/> if it did not exist.</returns>
    /// <exception cref="InvalidOperationException">The container exists but cannot be deleted
    /// (for example, it still has children). The endpoints translate this to
    /// <c>409 Conflict</c>.</exception>
    /// <exception cref="WopiResourceLockedException">The container is WOPI-locked. Thrown by the
    /// lock-aware storage decorator when registered.</exception>
    Task<bool> DeleteWopiContainer(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames an existing file.
    /// </summary>
    /// <remarks>
    /// The endpoints map a <see langword="false"/> result to <c>404 Not Found</c>.
    /// </remarks>
    /// <param name="identifier">The identifier of the file.</param>
    /// <param name="requestedName">The new file name, including the extension.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the file was renamed; <see langword="false"/> if it does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="requestedName"/> is not a valid file name.</exception>
    /// <exception cref="InvalidOperationException">A file named <paramref name="requestedName"/>
    /// already exists. The endpoints translate this to <c>409 Conflict</c> with
    /// <c>X-WOPI-InvalidFileNameError</c>.</exception>
    /// <exception cref="WopiResourceLockedException">The file is WOPI-locked. Thrown by the
    /// lock-aware storage decorator when registered.</exception>
    Task<bool> RenameWopiFile(string identifier, string requestedName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames an existing container.
    /// </summary>
    /// <remarks>
    /// The endpoints map a <see langword="false"/> result to <c>404 Not Found</c>.
    /// </remarks>
    /// <param name="identifier">The identifier of the container.</param>
    /// <param name="requestedName">The new container name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the container was renamed; <see langword="false"/> if it does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="requestedName"/> is not a valid container name.</exception>
    /// <exception cref="InvalidOperationException">A container named <paramref name="requestedName"/>
    /// already exists. The endpoints translate this to <c>409 Conflict</c>.</exception>
    /// <exception cref="WopiResourceLockedException">The container is WOPI-locked. Thrown by the
    /// lock-aware storage decorator when registered.</exception>
    Task<bool> RenameWopiContainer(string identifier, string requestedName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether <paramref name="name"/> is acceptable as a file name.
    /// </summary>
    /// <remarks>
    /// The name must be a single segment: at minimum not blank, free of <c>/</c> and control
    /// characters, not <c>.</c> or <c>..</c>, and at most <see cref="FileNameMaxLength"/>
    /// characters long. Providers may apply additional constraints (the file-system provider
    /// rejects whatever <c>Path.GetInvalidFileNameChars()</c> reports for the current OS, which on
    /// Windows includes <c>\</c> and other reserved characters; the Azure provider rejects
    /// <c>\</c> on every platform for portability).
    /// </remarks>
    /// <param name="name">The candidate file name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the name is valid; otherwise, <see langword="false"/>.</returns>
    Task<bool> CheckValidFileName(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether <paramref name="name"/> is acceptable as a container name.
    /// </summary>
    /// <remarks>
    /// The same length and single-segment constraints as <see cref="CheckValidFileName"/> apply;
    /// providers diverge between the two only for extension-rule differences (for example, a
    /// host that bans leading-dot file names but allows leading-dot directory names).
    /// </remarks>
    /// <param name="name">The candidate container name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the name is valid; otherwise, <see langword="false"/>.</returns>
    Task<bool> CheckValidContainerName(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a file name, based on <paramref name="name"/>, that is free in the container
    /// <paramref name="containerId"/>.
    /// </summary>
    /// <param name="containerId">The identifier of the parent container.</param>
    /// <param name="name">The candidate file name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><paramref name="name"/> unchanged if no file with that name exists; otherwise a
    /// deduplicated variant (typically <c>"name (1).ext"</c>, <c>"name (2).ext"</c>, …).</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid file name.</exception>
    Task<string> GetSuggestedFileName(string containerId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a container name, based on <paramref name="name"/>, that is free in the container
    /// <paramref name="containerId"/>.
    /// </summary>
    /// <param name="containerId">The identifier of the parent container.</param>
    /// <param name="name">The candidate container name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><paramref name="name"/> unchanged if no container with that name exists;
    /// otherwise a deduplicated variant.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid container name.</exception>
    Task<string> GetSuggestedContainerName(string containerId, string name, CancellationToken cancellationToken = default);
}
