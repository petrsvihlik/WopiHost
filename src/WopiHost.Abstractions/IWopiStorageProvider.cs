using System.Collections.ObjectModel;

namespace WopiHost.Abstractions;

/// <summary>
/// Implementation of read operations for an external storage provider.
/// </summary>
public interface IWopiStorageProvider
{
    /// <summary>
    /// Returns a concrete instance of an implementation of the <see cref="IWopiFile"/>.
    /// </summary>
    /// <param name="identifier">Generic string identifier of a file (typically some kind of a path).</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>Instance of a file.</returns>
    Task<IWopiFile?> GetWopiFile(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a concrete instance of an implementation of the <see cref="IWopiFolder"/>.
    /// </summary>
    /// <param name="identifier">Generic string identifier of a container (typically some kind of a path).</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>Instance of a container.</returns>
    Task<IWopiFolder?> GetWopiContainer(string? identifier = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all files from the given source.
    /// </summary>
    /// <param name="identifier">Container identifier (use null for root)</param>
    /// <param name="searchPattern">search pattern for files</param>
    /// <param name="cancellationToken">cancellation token</param>
    IAsyncEnumerable<IWopiFile> GetWopiFiles(string? identifier = null, string? searchPattern = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all containers from the given source.
    /// </summary>
    /// <param name="identifier">Container identifier (use null for root)</param>
    /// <param name="cancellationToken">cancellation token</param>
    IAsyncEnumerable<IWopiFolder> GetWopiContainers(string? identifier = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reference to the root container.
    /// </summary>
    IWopiFolder RootContainerPointer { get; }

    /// <summary>
    /// Returns the ancestors of the given container or file.
    /// </summary>
    /// <param name="resourceType">type of resource the identifier is pointing to</param>
    /// <param name="identifier">Container/File identifier.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>list of containers top-down excluding the specified identifier</returns>
    Task<ReadOnlyCollection<IWopiFolder>> GetAncestors(WopiResourceType resourceType, string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a WOPI resource by its name.
    /// </summary>
    /// <param name="resourceType">what kind of Wopi resource are we looking for (Container or File)</param>
    /// <param name="containerId">parent containerId to search within</param>
    /// <param name="name">the exact name to look for</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns></returns>
    Task<IWopiResource?> GetWopiResourceByName(WopiResourceType resourceType, string containerId, string name, CancellationToken cancellationToken = default);
}