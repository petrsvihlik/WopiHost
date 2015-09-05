using System.Collections.Generic;
using System.IO;
using Microsoft.Framework.Configuration;
using WopiHost.Contracts;

namespace WopiFileSystemProvider
{
    public class WopiFileSystemProvider : IWopiFileProvider
    {
        public IConfiguration Configuration { get; set; }
        public IConfigurationSection WopiRootPath => Configuration.GetSection("WopiRootPath");

        public WopiFileSystemProvider(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IWopiFile GetWopiFile(string identifier)
        {
            return new WopiFile(WopiRootPath.Value + Path.DirectorySeparatorChar + identifier, identifier);
        }

        public List<IWopiFile> GetWopiFiles()
        {
            List<IWopiFile> files = new List<IWopiFile>();
            foreach (string path in Directory.GetFiles(WopiRootPath.Value))
            {
                files.Add(GetWopiFile(Path.GetFileName(path)));
            }
            return files;
        }
    }
}
