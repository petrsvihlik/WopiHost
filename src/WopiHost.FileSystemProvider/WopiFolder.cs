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
		private DirectoryInfo _folderInfo;

    /// <inheritdoc/>
    protected string Path { get; set; } = path;

    /// <inheritdoc/>
    protected DirectoryInfo FolderInfo => _folderInfo ??= new DirectoryInfo(Path);

		/// <inheritdoc/>
		public string Name => FolderInfo.Name;

    /// <inheritdoc/>
    public string Identifier { get; } = folderIdentifier;
}
