namespace WopiHost.Abstractions;

/// <summary>
/// Represents a container of files and child containers.
/// </summary>
/// <remarks>
/// <see cref="Size"/> and <see cref="ChildCount"/> are derivable by any backend that can
/// enumerate (file system: <see cref="System.IO.DirectoryInfo"/>; blob: prefix scan); providers
/// may compute them lazily or eagerly as the underlying store allows.
/// </remarks>
public interface IWopiContainer : IWopiResource
{
    /// <summary>
    /// Gets the total size, in bytes, of the container's contents, including all descendants.
    /// </summary>
    /// <remarks>
    /// Reading this may enumerate every descendant (O(N) in the file/blob count), so treat it as
    /// cold-path metadata rather than reading it inside tight loops.
    /// </remarks>
    long Size { get; }

    /// <summary>
    /// Gets the number of direct child resources (files plus immediate sub-containers, not
    /// recursive). A container with three files and two sub-containers reports <c>5</c>,
    /// regardless of what those sub-containers hold.
    /// </summary>
    int ChildCount { get; }
}
