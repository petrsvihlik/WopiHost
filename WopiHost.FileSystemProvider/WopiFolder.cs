using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider;

/// <inheritdoc/>
public class WopiFolder : IWopiFolder
	{
		private DirectoryInfo _folderInfo;

		/// <inheritdoc/>
		protected string Path { get; set; }

		/// <inheritdoc/>
		protected DirectoryInfo FolderInfo => _folderInfo ??= new DirectoryInfo(Path);

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
			Path = path;
			Identifier = folderIdentifier;
		}
	}
