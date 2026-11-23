using System.Collections.ObjectModel;

namespace WopiHost.Abstractions;

/// <summary>
/// Provides read access to files and containers in a storage backend.
/// </summary>
/// <remarks>
/// Write operations live on <see cref="IWopiWritableStorageProvider"/>.
/// </remarks>
public interface IWopiStorageProvider
{
    /// <summary>
    /// Gets the root container of this storage provider.
    /// </summary>
    /// <remarks>
    /// Its <see cref="IWopiResource.Identifier"/> is the only way to address the root in APIs
    /// that take a container identifier (a <see langword="null"/> identifier is not accepted);
    /// use <see cref="IWopiResource.Name"/> for UI surfaces such as breadcrumbs.
    /// </remarks>
    IWopiContainer RootContainer { get; }

    /// <summary>
    /// Gets the file identified by <paramref name="identifier"/>.
    /// </summary>
    /// <param name="identifier">The identifier of the file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The file, or <see langword="null"/> if no file with that id exists.</returns>
    Task<IWopiFile?> GetWopiFile(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the container identified by <paramref name="identifier"/>.
    /// </summary>
    /// <param name="identifier">The identifier of the container.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The container, or <see langword="null"/> if no container with that id exists.</returns>
    Task<IWopiContainer?> GetWopiContainer(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates the files directly in the container identified by <paramref name="identifier"/>,
    /// optionally filtered by file extension.
    /// </summary>
    /// <param name="identifier">The identifier of the container. Pass
    /// <c><see cref="RootContainer"/>.Identifier</c> to enumerate the root.</param>
    /// <param name="fileExtensions">
    /// The file extensions to include, or <see langword="null"/> or empty to return every file.
    /// Each element must be a leading-dot extension (<c>".docx"</c>, not <c>"docx"</c>); matching
    /// is case-insensitive, per the WOPI <c>X-WOPI-FileExtensionFilterList</c> spec. Wildcard
    /// handling is provider-specific: the file-system provider passes each extension into an OS
    /// search pattern, where <c>*</c> and <c>?</c> act as wildcards.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The files in the container that match the filter.</returns>
    /// <remarks>
    /// Implementations push filtering as close to the underlying storage as the backend allows
    /// (the file-system provider uses a per-extension <c>Directory.EnumerateFiles</c> glob; the
    /// Azure provider filters items as they stream from <c>GetBlobsByHierarchyAsync</c>, since the
    /// Blob list API offers only a prefix filter). Callers should not post-filter the result by
    /// extension.
    /// </remarks>
    IAsyncEnumerable<IWopiFile> GetWopiFiles(string identifier, IReadOnlyCollection<string>? fileExtensions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates the containers directly in the container identified by <paramref name="identifier"/>.
    /// </summary>
    /// <param name="identifier">The identifier of the parent container. Pass
    /// <c><see cref="RootContainer"/>.Identifier</c> to enumerate the root.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The child containers.</returns>
    IAsyncEnumerable<IWopiContainer> GetWopiContainers(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the ancestor containers of the file identified by <paramref name="fileId"/>.
    /// </summary>
    /// <remarks>
    /// Matches the WOPI <c>AncestorsWithRootFirst</c> contract for the
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/enumerateancestors">files EnumerateAncestors</see>
    /// operation: <c>/root/grandparent/parent/myfile.docx</c> returns
    /// <c>[root, grandparent, parent]</c>.
    /// </remarks>
    /// <param name="fileId">The identifier of the file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The ancestors, root first, <i>including the immediate parent</i> and excluding
    /// the file itself.</returns>
    Task<ReadOnlyCollection<IWopiContainer>> GetFileAncestors(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the ancestor containers of the container identified by <paramref name="containerId"/>.
    /// </summary>
    /// <remarks>
    /// Matches the WOPI <c>AncestorsWithRootFirst</c> contract for the
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumerateancestors">containers EnumerateAncestors</see>
    /// operation: <c>/root/grandparent/parent/mycontainer</c> returns
    /// <c>[root, grandparent, parent]</c>.
    /// </remarks>
    /// <param name="containerId">The identifier of the container.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The ancestors, root first, <i>including the immediate parent</i> and excluding
    /// the container itself; empty for the root.</returns>
    Task<ReadOnlyCollection<IWopiContainer>> GetContainerAncestors(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the file with the given name directly in the container <paramref name="containerId"/>.
    /// </summary>
    /// <param name="containerId">The identifier of the parent container to search.</param>
    /// <param name="name">The exact name to look for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The file, or <see langword="null"/> if the parent container does not exist or
    /// holds no file with that name.</returns>
    Task<IWopiFile?> GetWopiFileByName(string containerId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the child container with the given name directly in the container <paramref name="containerId"/>.
    /// </summary>
    /// <param name="containerId">The identifier of the parent container to search.</param>
    /// <param name="name">The exact name to look for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The container, or <see langword="null"/> if the parent container does not exist
    /// or holds no child container with that name.</returns>
    Task<IWopiContainer?> GetWopiContainerByName(string containerId, string name, CancellationToken cancellationToken = default);
}
