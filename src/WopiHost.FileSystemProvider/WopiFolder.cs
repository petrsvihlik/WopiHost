using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <inheritdoc/>
public class WopiFolder : IWopiFolder
{
    /// <inheritdoc/>
    private readonly DirectoryInfo FolderInfo;

	/// <inheritdoc/>
	public string Name => FolderInfo.Name;

    /// <inheritdoc/>
    public string Identifier { get; }

    /// <summary>
    /// Creates an instance of <see cref="WopiFolder"/>.
    /// </summary>
    /// <param name="path">Path on the file system the folder is located in.</param>
    /// <param name="folderIdentifier">A unique identifier of a folder.</param>
    public WopiFolder(string path, string folderIdentifier)
    {
        FolderInfo = new DirectoryInfo(path);
        Identifier = folderIdentifier;
    }
}
