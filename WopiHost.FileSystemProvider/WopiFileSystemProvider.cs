using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider
{
	public class WopiFileSystemProvider : IWopiFileProvider
	{
		protected IConfiguration Configuration { get; set; }
		protected IConfigurationSection WopiRootPath => Configuration.GetSection(nameof(WopiRootPath));
		protected IConfigurationSection WebRootPath => Configuration.GetSection(nameof(WebRootPath));

		protected string WopiAbsolutePath
		{
			get { return Path.IsPathRooted(WopiRootPath.Value) ? WopiRootPath.Value : Path.Combine(WebRootPath.Value, WopiRootPath.Value); }
		}

		public WopiFileSystemProvider(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IWopiFile GetWopiFile(string identifier)
		{
			return new WopiFile(Path.Combine(WopiAbsolutePath, identifier), identifier);
		}

		public IWopiItem GetWopiFolder(string identifier)
		{
			return new WopiFolder(Path.Combine(WopiAbsolutePath, identifier), identifier);
		}

		public List<IWopiFile> GetWopiFiles()
		{
			List<IWopiFile> files = new List<IWopiFile>();
			foreach (string path in Directory.GetFiles(WopiAbsolutePath))
			{
				files.Add(GetWopiFile(Path.GetFileName(path)));
			}
			return files;
		}

		public List<IWopiItem> GetWopiFolders()
		{
			List<IWopiItem> folders = new List<IWopiItem>();
			foreach (string directory in Directory.GetDirectories(WopiAbsolutePath))
			{
				folders.Add(GetWopiFolder(directory.Remove(0, directory.LastIndexOf(Path.DirectorySeparatorChar))));
			}
			return folders;
		}

		public List<IWopiItem> GetWopiItems()
		{
			return GetWopiFolders().Union(GetWopiFiles()).ToList();
		} 
	}
}
