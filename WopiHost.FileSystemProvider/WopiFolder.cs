using System.IO;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider
{
	public class WopiFolder : IWopiFolder
	{
		private DirectoryInfo _folderInfo;

		protected string Path { get; set; }

        protected DirectoryInfo FolderInfo => _folderInfo ??= new DirectoryInfo(Path);
        public string Name => FolderInfo.Name;

        public string Identifier { get; }

		public WopiFolder(string path, string fileIdentifier)
		{
			Path = path;
			Identifier = fileIdentifier;
		}
	}
}
