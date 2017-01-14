using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider
{
	public class WopiFileSystemProvider : IWopiStorageProvider
	{
		protected IConfiguration Configuration { get; set; }
		protected string WopiRootPath => Configuration.GetValue("WopiRootPath", string.Empty);

		/// <summary>
		/// Gets root path of the web application (e.g. IHostingEnvironment.WebRootPath for .NET Core apps)
		/// </summary>
		protected string WebRootPath => Configuration.GetValue("WebRootPath", string.Empty);

		protected string WopiAbsolutePath
		{
			get { return Path.IsPathRooted(WopiRootPath) ? WopiRootPath : Path.Combine(WebRootPath, WopiRootPath); }
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
				string fileId = Path.Combine(identifier, Path.GetFileName(path));
				files.Add(GetWopiFile(fileId));
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
