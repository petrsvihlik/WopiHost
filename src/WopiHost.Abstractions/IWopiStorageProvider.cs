using System.Collections.ObjectModel;

namespace WopiHost.Abstractions;

/// <summary>
/// Implementation of read operations for an external storage provider.
/// </summary>
public interface IWopiStorageProvider
{
    /// <summary>
    /// Returns a concrete instance of <see cref="IWopiFile"/> or <see cref="IWopiFolder"/>
    /// </summary>
    /// <param name="identifier">Generic string identifier of the Wopi resource.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>Instance of a file.</returns>
    Task<T?> GetWopiResource<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource;

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
    /// <param name="identifier">Container/File identifier.</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>list of containers top-down excluding the specified identifier</returns>
    Task<ReadOnlyCollection<IWopiFolder>> GetAncestors<T>(string identifier, CancellationToken cancellationToken = default)
        where T : class, IWopiResource;

    /// <summary>
    /// Returns a Wopi resource by its name.
    /// </summary>
    /// <param name="containerId">parent containerId to search within</param>
    /// <param name="name">the exact name to look for</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>either <see cref="IWopiFile"/> or <see cref="IWopiFolder"/>, null if not found</returns>
    Task<T?> GetWopiResourceByName<T>(string containerId, string name, CancellationToken cancellationToken = default)
        where T : class, IWopiResource;
}