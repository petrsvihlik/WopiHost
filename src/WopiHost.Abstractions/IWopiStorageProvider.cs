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
    /// Returns all files contained by the container identified by <paramref name="identifier"/>.
    /// Pass <c><see cref="RootContainer"/>.Identifier</c> to enumerate the root.
    /// </summary>
    /// <param name="identifier">Container identifier. Required.</param>
    /// <param name="searchPattern">search pattern for files</param>
    /// <param name="cancellationToken">cancellation token</param>
    IAsyncEnumerable<IWopiFile> GetWopiFiles(string identifier, string? searchPattern = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all containers contained by the container identified by <paramref name="identifier"/>.
    /// Pass <c><see cref="RootContainer"/>.Identifier</c> to enumerate the root.
    /// </summary>
    /// <param name="identifier">Container identifier. Required.</param>
    /// <param name="cancellationToken">cancellation token</param>
    IAsyncEnumerable<IWopiFolder> GetWopiContainers(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// The root container of this storage provider. Use <see cref="IWopiResource.Identifier"/>
    /// to refer to the root in any API that takes a container identifier; use
    /// <see cref="IWopiResource.Name"/> for UI surfaces (breadcrumbs etc.).
    /// </summary>
    /// <remarks>
    /// The single canonical way to address the root. Earlier revisions also accepted a <see langword="null"/>
    /// container identifier as "root" on <see cref="GetWopiFiles"/> / <see cref="GetWopiContainers"/> /
    /// <see cref="IWopiWritableStorageProvider.CreateWopiChildResource{T}"/>; that sugar was removed
    /// in favour of this property to keep one obvious way to spell the same thing.
    /// </remarks>
    IWopiFolder RootContainer { get; }

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