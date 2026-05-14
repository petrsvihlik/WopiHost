namespace WopiHost.Abstractions;

/// <summary>
/// Implementation of writable operations for an external storage provider.
/// </summary>
public interface IWopiWritableStorageProvider
{
    /// <summary>
    /// An integer value that indicates the maximum length for file names that the WOPI host supports, excluding the file extension. 
    /// The default value is 250. Note that WOPI clients will use this default value if the property is omitted or if it is explicitly set to <c>0</c>.
    /// </summary>
    int FileNameMaxLength { get; }

    /// <summary>
    /// Creates a new Wopi resource (Container or File) in the specified container.
    /// </summary>
    /// <param name="containerId">
    /// Identifier of the parent container. Required. Pass
    /// <c><see cref="IWopiStorageProvider.RootContainer"/>.Identifier</c> to create directly under
    /// the root.
    /// </param>
    /// <param name="name">the new resource's name</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>identifier of new resource</returns>
    /// <remarks>creating a <see cref="WopiResourceType.File"/> always creates a 0-byte file</remarks>
    Task<T?> CreateWopiChildResource<T>(
        string containerId,
        string name,
        CancellationToken cancellationToken = default)
        where T : class, IWopiResource;

    /// <summary>
    /// Deletes the specified Wopi resource (container or file).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contract for the missing/exception split (clarified in #380 item 4.2 so the two in-tree
    /// providers and any future impl behave the same):
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// Returns <see langword="false"/> when the <paramref name="identifier"/> does not resolve
    /// to a resource (the underlying store has no such file/container). Controllers map this
    /// to <c>404 Not Found</c> per the WOPI spec.
    /// </item>
    /// <item>
    /// Throws <see cref="InvalidOperationException"/> when the resource exists but cannot be
    /// deleted (e.g. a container that still has children). Controllers translate this to
    /// <c>409 Conflict</c>.
    /// </item>
    /// <item>
    /// <see cref="WopiResourceLockedException"/> surfaces through the lock-aware decorator when
    /// the resource is currently WOPI-locked; controllers translate to <c>409 Conflict</c> with
    /// the X-WOPI-Lock response header.
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="identifier">Generic string identifier of a container (typically some kind of a path).</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns><see langword="true"/> if the resource existed and was deleted; <see langword="false"/> if it did not exist.</returns>
    Task<bool> DeleteWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource;

    /// <summary>
    /// Renames an existing Wopi resource (container or file).
    /// </summary>
    /// <remarks>
    /// Same contract shape as <see cref="DeleteWopiResource{T}"/>: returns <see langword="false"/>
    /// when <paramref name="identifier"/> does not resolve to a resource (→ 404), throws
    /// <see cref="ArgumentException"/> for bad names and <see cref="InvalidOperationException"/>
    /// when the target name already exists (→ 409 with <c>X-WOPI-InvalidFileNameError</c>).
    /// </remarks>
    /// <param name="identifier">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="requestedName">A string that is a container name. Required.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns><see langword="true"/> if the resource existed and was renamed; <see langword="false"/> if it did not exist.</returns>
    Task<bool> RenameWopiResource<T>(string identifier, string requestedName, CancellationToken cancellationToken = default)
        where T : class, IWopiResource;

    /// <summary>
    /// Checks if the name is valid for the specified resource type.
    /// </summary>
    /// <param name="name">possible name</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>true if valid, false if otherwise</returns>
    Task<bool> CheckValidName<T>(
        string name,
        CancellationToken cancellationToken = default)
        where T : class, IWopiResource;

    /// <summary>
    /// Returns a suggested name for the specified resource type in the target containerId.
    /// </summary>
    /// <param name="containerId">parent container id</param>
    /// <param name="name">suggested name</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>if the proposed resource name does not exist yet it will return name, otherwise will suggest a new name</returns>
    Task<string> GetSuggestedName<T>(
        string containerId,
        string name,
        CancellationToken cancellationToken = default)
        where T : class, IWopiResource;
}