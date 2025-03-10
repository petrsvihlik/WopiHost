using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WopiHost.Abstractions;

/// <summary>
/// Implementation of writable operations for an external storage provider.
/// </summary>
public interface IWopiWritableStorageProvider
{
    /// <summary>
    /// Creates a new container in the specified container.
    /// </summary>
    /// <param name="identifier">Generic string identifier of a container (typically some kind of a path).</param>
    /// <param name="name">the new Container's name</param>
    /// <param name="isExactName">whether the container's name is a suggestion or must be left as-is</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>identifier of new container</returns>
    Task<string?> CreateWopiChildContainer(
        string identifier,
        string name,
        bool isExactName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified Wopi resource (container or file).
    /// </summary>
    /// <param name="resourceType">which Wopi resource to delete</param>
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
}