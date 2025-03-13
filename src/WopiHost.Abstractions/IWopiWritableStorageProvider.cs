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
    /// <param name="name">the new Container's name</param>
    /// <param name="containerId">identifier of parent container (or null to use root)</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>identifier of new resource</returns>
    /// <remarks>creating a <see cref="WopiResourceType.File"/> always creates a 0-byte file</remarks>
    Task<T?> CreateWopiChildResource<T>(
        string? containerId,
        string name,
        CancellationToken cancellationToken = default)
        where T : class, IWopiResource;

    /// <summary>
    /// Deletes the specified Wopi resource (container or file).
    /// </summary>
    /// <param name="identifier">Generic string identifier of a container (typically some kind of a path).</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>true for success</returns>
    Task<bool> DeleteWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource;

    /// <summary>
    /// Renames an existing Wopi resource (container or file).
    /// </summary>
    /// <param name="identifier">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="requestedName">A string that is a container name. Required.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>true for success</returns>
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