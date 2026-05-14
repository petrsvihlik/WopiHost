using System.Collections.ObjectModel;

namespace WopiHost.Abstractions;

/// <summary>
/// Implementation of read operations for an external storage provider.
/// </summary>
/// <remarks>
/// Per #420 item 1.1, file and container access points are exposed as distinct typed methods
/// rather than a single generic <c>GetWopiResource&lt;T&gt;</c> that used <c>typeof(T)</c> as a
/// runtime discriminator. Each method returns the precise resource interface; there are no
/// scenarios where a caller wants the union of both.
/// </remarks>
public interface IWopiStorageProvider
{
    /// <summary>
    /// The root container of this storage provider. Use <see cref="IWopiResource.Identifier"/>
    /// to refer to the root in any API that takes a container identifier; use
    /// <see cref="IWopiResource.Name"/> for UI surfaces (breadcrumbs etc.).
    /// </summary>
    /// <remarks>
    /// The single canonical way to address the root. Earlier revisions also accepted a <see langword="null"/>
    /// container identifier as "root" on <see cref="GetWopiFiles"/> / <see cref="GetWopiContainers"/> /
    /// <see cref="IWopiWritableStorageProvider.CreateWopiChildFile"/>; that sugar was removed
    /// in favour of this property to keep one obvious way to spell the same thing.
    /// </remarks>
    IWopiContainer RootContainer { get; }

    /// <summary>
    /// Returns the file identified by <paramref name="identifier"/>, or <see langword="null"/>
    /// if no file with that id exists.
    /// </summary>
    /// <param name="identifier">File identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IWopiFile?> GetWopiFile(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the container identified by <paramref name="identifier"/>, or <see langword="null"/>
    /// if no container with that id exists.
    /// </summary>
    /// <param name="identifier">Container identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IWopiContainer?> GetWopiContainer(string identifier, CancellationToken cancellationToken = default);

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
    /// <param name="cancellationToken">Cancellation token.</param>
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
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<IWopiContainer> GetWopiContainers(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the ancestor containers of the file identified by <paramref name="fileId"/>,
    /// root-first, <em>including the immediate parent</em>, excluding the file itself.
    /// </summary>
    /// <remarks>
    /// Matches the WOPI <c>AncestorsWithRootFirst</c> contract for the
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/enumerateancestors">files EnumerateAncestors</see>
    /// operation: <c>/root/grandparent/parent/myfile.docx</c> returns
    /// <c>[root, grandparent, parent]</c>.
    /// </remarks>
    /// <param name="fileId">File identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ReadOnlyCollection<IWopiContainer>> GetFileAncestors(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the ancestor containers of the container identified by <paramref name="containerId"/>,
    /// root-first, <em>including the immediate parent</em>, excluding the container itself.
    /// </summary>
    /// <remarks>
    /// Matches the WOPI <c>AncestorsWithRootFirst</c> contract for the
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/containers/enumerateancestors">containers EnumerateAncestors</see>
    /// operation: <c>/root/grandparent/parent/mycontainer</c> returns
    /// <c>[root, grandparent, parent]</c>. Returns an empty collection when called on the root.
    /// </remarks>
    /// <param name="containerId">Container identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ReadOnlyCollection<IWopiContainer>> GetContainerAncestors(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the file by name within <paramref name="containerId"/>.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when either the parent container itself does not exist
    /// or the file is not present under it. Consistent with <see cref="GetWopiFile"/>'s
    /// null-on-missing behaviour (#380 item 4.2).
    /// </remarks>
    /// <param name="containerId">Parent container id to search within.</param>
    /// <param name="name">The exact name to look for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IWopiFile?> GetWopiFileByName(string containerId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the container by name within <paramref name="containerId"/>.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when either the parent container itself does not exist
    /// or the child container is not present under it. Consistent with <see cref="GetWopiContainer"/>'s
    /// null-on-missing behaviour (#380 item 4.2).
    /// </remarks>
    /// <param name="containerId">Parent container id to search within.</param>
    /// <param name="name">The exact name to look for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IWopiContainer?> GetWopiContainerByName(string containerId, string name, CancellationToken cancellationToken = default);
}
