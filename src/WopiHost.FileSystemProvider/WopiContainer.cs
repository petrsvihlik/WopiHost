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
}
