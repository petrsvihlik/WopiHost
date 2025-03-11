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
    /// <param name="resourceType">which kind Wopi resource type do we want name</param>
    /// <param name="name">the new Container's name</param>
    /// <param name="containerId">identifier of parent container (or null to use root)</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>identifier of new container</returns>
    Task<IWopiResource?> CreateWopiChildResource(
        WopiResourceType resourceType,
        string? containerId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified Wopi resource (container or file).
    /// </summary>
    /// <param name="resourceType">which kind of Wopi resource to delete</param>
    /// <param name="identifier">Generic string identifier of a container (typically some kind of a path).</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>true for success</returns>
    Task<bool> DeleteWopiResource(WopiResourceType resourceType, string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames an existing Wopi resource (container or file).
    /// </summary>
    /// <param name="resourceType">which Wopi resource to rename</param>
    /// <param name="identifier">A string that specifies a container ID of a container managed by host. This string must be URL safe.</param>
    /// <param name="requestedName">A UTF-7 encoded string that is a container name. Required.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>true for success</returns>
    Task<bool> RenameWopiResource(WopiResourceType resourceType, string identifier, string requestedName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the name is valid for the specified resource type.
    /// </summary>
    /// <param name="resourceType">which kind of Wopi resource to check against</param>
    /// <param name="name">possible name</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>true if valid, false if otherwise</returns>
    Task<bool> CheckValidName(
        WopiResourceType resourceType,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a suggested name for the specified resource type in the target containerId.
    /// </summary>
    /// <param name="resourceType">which kind Wopi resource type do we want name</param>
    /// <param name="containerId">parent container id</param>
    /// <param name="name">suggested name</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    Task<string> GetSuggestedName(
        WopiResourceType resourceType,
        string containerId,
        string name,
        CancellationToken cancellationToken = default);
}