namespace WopiHost.Abstractions;

/// <summary>
/// Object that represents a container with files.
/// </summary>
/// <remarks>
/// #425 item 1.4: the type carries two metadata members (<see cref="Size"/>,
/// <see cref="ChildCount"/>) so a marker interface doesn't just exist to tag the kind.
/// The two members are implementable by every storage backend that can already enumerate
/// (file system: <see cref="System.IO.DirectoryInfo"/>; blob: prefix-scan); providers may
/// compute lazily or eagerly as the underlying store allows.
/// </remarks>
public interface IWopiContainer : IWopiResource
{
    /// <summary>
    /// Total size of the container's contents in bytes, including all descendants
    /// (recursive). Providers compute this from their underlying enumeration — the call may
    /// be O(N) on the container's blob/file count, so callers should treat it as cold-path
    /// metadata rather than reading it inside tight loops.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Number of direct child resources (files plus immediate sub-containers, no recursion).
    /// A container with three files and two sub-containers reports <c>5</c>, regardless of
    /// what's inside those sub-containers.
    /// </summary>
    int ChildCount { get; }
}
