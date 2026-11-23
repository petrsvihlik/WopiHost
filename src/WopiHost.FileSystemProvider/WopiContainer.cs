using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <summary>
/// Represents a folder on the file system as an <see cref="IWopiContainer"/>.
/// </summary>
/// <param name="path">The file system path of the folder.</param>
/// <param name="folderIdentifier">The unique identifier of the folder.</param>
public class WopiContainer(string path, string folderIdentifier) : IWopiContainer
{
    private readonly DirectoryInfo _folderInfo = new(path);

    /// <inheritdoc/>
    public string Name => _folderInfo.Name;

    /// <inheritdoc/>
    public string Identifier { get; } = folderIdentifier;

    /// <inheritdoc/>
    /// <remarks>
    /// Recomputed each access via <see cref="DirectoryInfo.EnumerateFiles(string, SearchOption)"/>.
    /// Cold-path metadata — bounded by the file count under this folder. Returns <c>0</c>
    /// when the folder no longer exists rather than throwing, so callers building response
    /// payloads don't have to special-case stale ids.
    /// </remarks>
    public long Size => _folderInfo.Exists
        ? _folderInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
        : 0L;

    /// <inheritdoc/>
    /// <remarks>
    /// Counts direct child files and folders. Returns <c>0</c> when the folder no longer exists.
    /// </remarks>
    public int ChildCount => _folderInfo.Exists
        ? _folderInfo.EnumerateFileSystemInfos().Count()
        : 0;
}
