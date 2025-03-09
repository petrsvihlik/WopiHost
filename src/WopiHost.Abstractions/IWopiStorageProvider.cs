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
    /// <returns>Instance of a file.</returns>
    IWopiFile GetWopiFile(string identifier);

    /// <summary>
    /// Returns a concrete instance of an implementation of the <see cref="IWopiFolder"/>.
    /// </summary>
    /// <param name="identifier">Generic string identifier of a container (typically some kind of a path).</param>
    /// <returns>Instance of a container.</returns>
    IWopiFolder GetWopiContainer(string identifier = "");

    /// <summary>
    /// Returns all files from the given source.
    /// This method is very likely to change in the future.
    /// </summary>
    /// <param name="identifier">Container identifier (use null for root)</param>
    ReadOnlyCollection<IWopiFile> GetWopiFiles(string identifier = "");

    /// <summary>
    /// Returns all containers from the given source.
    /// This method is very likely to change in the future.
    /// </summary>
    /// <param name="identifier">Container identifier (use null for root)</param>
    ReadOnlyCollection<IWopiFolder> GetWopiContainers(string identifier = "");

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
}