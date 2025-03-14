using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <inheritdoc/>
/// <summary>
/// Creates an instance of <see cref="WopiFolder"/>.
/// </summary>
/// <param name="path">Path on the file system the folder is located in.</param>
/// <param name="folderIdentifier">A unique identifier of a folder.</param>
public class WopiFolder(string path, string folderIdentifier) : IWopiFolder
{
    /// <inheritdoc/>
    private readonly DirectoryInfo FolderInfo = new(path);

	/// <inheritdoc/>
	public string Name => FolderInfo.Name;

    /// <inheritdoc/>
    public string Identifier { get; } = folderIdentifier;
}
