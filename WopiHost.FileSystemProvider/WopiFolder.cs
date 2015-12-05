using System.IO;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider
{
	public class WopiFolder : IWopiItem
	{
		protected DirectoryInfo folderInfo;
		protected string Path { get; set; }
		protected DirectoryInfo FolderInfo
		{
			get { return folderInfo ?? (folderInfo = new DirectoryInfo(Path)); }
		}
		public string Name { get { return FolderInfo.Name; } }
		public string Identifier { get; }
		public WopiItemType WopiItemType { get { return WopiItemType.Folder; } }

		public WopiFolder(string path, string fileIdentifier)
		{
			Path = path;
			Identifier = fileIdentifier;
		}
	}
}
