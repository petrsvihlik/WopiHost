using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider
{
	public class WopiFileSystemProvider : IWopiStorageProvider
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

		public IWopiFolder GetWopiContainer(string identifier = "")
		{
			return new WopiFolder(Path.Combine(WopiAbsolutePath, identifier), identifier);
		}

		public List<IWopiFile> GetWopiFiles(string identifier = "")
		{
			List<IWopiFile> files = new List<IWopiFile>();
			foreach (string path in Directory.GetFiles(Path.Combine(WopiAbsolutePath, identifier)))
			{
				files.Add(GetWopiFile(Path.GetFileName(path)));
			}
			return files;
		}

		public List<IWopiFolder> GetWopiContainers(string identifier = "")
		{
			List<IWopiFolder> folders = new List<IWopiFolder>();
			foreach (string directory in Directory.GetDirectories(Path.Combine(WopiAbsolutePath, identifier)))
			{
				folders.Add(GetWopiContainer("." + directory.Remove(0, directory.LastIndexOf(Path.DirectorySeparatorChar))));
			}
			return folders;
		}
	}
}
