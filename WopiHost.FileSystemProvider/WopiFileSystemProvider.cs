using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Hosting;
using WopiHost.Abstractions;
using Microsoft.Extensions.Configuration;

namespace WopiHost.FileSystemProvider
{
    /// <summary>
    /// Provides files and folders based on a base64-encoded paths.
    /// </summary>
    public class WopiFileSystemProvider : IWopiStorageProvider
    {
        public WopiFileSystemProviderOptions FileSystemProviderOptions { get; }

        private readonly string ROOT_PATH = @".\";

        /// <summary>
        /// Reference to the root container.
        /// </summary>
        public IWopiFolder RootContainerPointer => new WopiFolder(ROOT_PATH, EncodeIdentifier(ROOT_PATH));

        protected string WopiRootPath => FileSystemProviderOptions.RootPath;

        /// <summary>
        /// Context of the hosting environment.
        /// </summary>
        protected IHostEnvironment HostEnvironment { get; set; }

        protected string WopiAbsolutePath => Path.IsPathRooted(WopiRootPath) ? WopiRootPath : Path.Combine(HostEnvironment.ContentRootPath, WopiRootPath);

        public WopiFileSystemProvider(IHostEnvironment env, IConfiguration configuration)
        {
            if (env is null)
            {
                throw new ArgumentNullException(nameof(env));
            }

            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            HostEnvironment = env;
            FileSystemProviderOptions = configuration.GetSection(WopiConfigurationSections.STORAGE_OPTIONS).Get<WopiFileSystemProviderOptions>();
        }

        /// <summary>
        /// Gets a file using an identifier.
        /// </summary>
        /// <param name="identifier">A base64-encoded file path.</param>
        public IWopiFile GetWopiFile(string identifier)
        {
            string filePath = DecodeIdentifier(identifier);
            return new WopiFile(Path.Combine(WopiAbsolutePath, filePath), identifier);
        }

        /// <summary>
        /// Gets a folder using an identifier.
        /// </summary>
        /// <param name="identifier">A base64-encoded folder path.</param>
        public IWopiFolder GetWopiContainer(string identifier = "")
        {
            string folderPath = DecodeIdentifier(identifier);
            return new WopiFolder(Path.Combine(WopiAbsolutePath, folderPath), identifier);
        }

        /// <summary>
        /// Gets all files in a folder.
        /// </summary>
        /// <param name="identifier">A base64-encoded folder path.</param>
        public List<IWopiFile> GetWopiFiles(string identifier = "")
        {
            string folderPath = DecodeIdentifier(identifier);
            List<IWopiFile> files = new List<IWopiFile>();
            foreach (string path in Directory.GetFiles(Path.Combine(WopiAbsolutePath, folderPath)))  //TODO Directory.Enumerate...
            {
                string filePath = Path.Combine(folderPath, Path.GetFileName(path));
                string fileId = EncodeIdentifier(filePath);
                files.Add(GetWopiFile(fileId));
            }
            return files;
        }

        /// <summary>
        /// Gets all sub-folders of a folder.
        /// </summary>
        /// <param name="identifier">A base64-encoded folder path.</param>
        public List<IWopiFolder> GetWopiContainers(string identifier = "")
        {
            string folderPath = DecodeIdentifier(identifier);
            List<IWopiFolder> folders = new List<IWopiFolder>();
            foreach (string directory in Directory.GetDirectories(Path.Combine(WopiAbsolutePath, folderPath)))
            {
                var subfolderPath = "." + directory.Remove(0, directory.LastIndexOf(Path.DirectorySeparatorChar));
                string folderId = EncodeIdentifier(subfolderPath);
                folders.Add(GetWopiContainer(folderId));
            }
            return folders;
        }

        private string DecodeIdentifier(string identifier)
        {
            var bytes = Convert.FromBase64String(identifier);
            return Encoding.UTF8.GetString(bytes);
        }

        private string EncodeIdentifier(string path)
        {
            var bytes = Encoding.UTF8.GetBytes(path);
            return Convert.ToBase64String(bytes);
        }
    }
}
