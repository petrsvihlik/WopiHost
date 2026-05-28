using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <inheritdoc/>
/// <summary>
/// Creates an instance of <see cref="WopiContainer"/>.
/// </summary>
/// <param name="path">Path on the file system the folder is located in.</param>
/// <param name="folderIdentifier">A unique identifier of a folder.</param>
public class WopiContainer(string path, string folderIdentifier) : IWopiContainer
{
    /// <inheritdoc/>
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
    public int ChildCount => _folderInfo.Exists
        ? _folderInfo.EnumerateFileSystemInfos().Count()
        : 0;
}
