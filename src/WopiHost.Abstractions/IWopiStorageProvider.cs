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
    /// Returns the files contained by the container identified by <paramref name="identifier"/>,
    /// optionally filtered by file extension. Pass <c><see cref="RootContainer"/>.Identifier</c>
    /// to enumerate the root.
    /// </summary>
    /// <param name="identifier">Container identifier. Required.</param>
    /// <param name="fileExtensions">
    /// Optional list of file extensions to include in the result. Each element must be a
    /// leading-dot extension (<c>".docx"</c>, not <c>"docx"</c>); matching is case-insensitive,
    /// per the WOPI <c>X-WOPI-FileExtensionFilterList</c> spec. When <see langword="null"/> or
    /// empty, every file in the container is returned. Wildcard characters in the elements
    /// are matched literally — the parameter is not a glob.
    /// </param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <remarks>
    /// Implementations are expected to push filtering as close to the underlying storage as
    /// the backend allows: the filesystem provider uses <c>Directory.EnumerateFiles</c> with a
    /// per-extension glob so the OS handles selection; the Azure-blob provider filters each
    /// item at the streaming-list boundary as items arrive from <c>GetBlobsByHierarchyAsync</c>,
    /// since the Blob list API exposes only a prefix filter at the wire level. Callers should
    /// not post-filter the returned <see cref="IAsyncEnumerable{T}"/> by extension — the
    /// provider has already done it.
    /// </remarks>
    IAsyncEnumerable<IWopiFile> GetWopiFiles(string identifier, IReadOnlyCollection<string>? fileExtensions = null, CancellationToken cancellationToken = default);

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
    /// Returns a Wopi resource by its name within a parent container.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when either the parent container itself does not exist
    /// or the child name is not present under it. Consistent with <see cref="GetWopiResource{T}"/>'s
    /// null-on-missing behaviour (#380 item 4.2): the two providers used to disagree —
    /// FileSystemProvider threw <see cref="DirectoryNotFoundException"/> on a missing parent;
    /// AzureStorageProvider returned null — and the inconsistency leaked through to callers
    /// (PutRelative resolution in particular).
    /// </remarks>
    /// <param name="containerId">parent containerId to search within</param>
    /// <param name="name">the exact name to look for</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>either <see cref="IWopiFile"/> or <see cref="IWopiFolder"/>, <see langword="null"/> if either the parent or the child is not present</returns>
    Task<T?> GetWopiResourceByName<T>(string containerId, string name, CancellationToken cancellationToken = default)
        where T : class, IWopiResource;
}