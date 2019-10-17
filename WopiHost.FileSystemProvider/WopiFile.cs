using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider
{
	public class WopiFile : IWopiFile
	{
		public string Identifier { get; }
		
		private FileInfo fileInfo;

		private FileVersionInfo fileVersionInfo;

		protected string FilePath { get; set; }

        protected FileInfo FileInfo => fileInfo ?? (fileInfo = new FileInfo(FilePath));

        protected FileVersionInfo FileVersionInfo => fileVersionInfo ?? (fileVersionInfo = FileVersionInfo.GetVersionInfo(FilePath));

        /// <inheritdoc />
        public bool Exists => FileInfo.Exists;

        public string Extension
		{
			get
			{
				var ext = FileInfo.Extension;
				if (ext.StartsWith(".", StringComparison.InvariantCulture))
				{
					ext = ext.Substring(1);
				}
				return ext;
			}
		}

        public string Version => FileVersionInfo.FileVersion ?? FileInfo.LastWriteTimeUtc.ToString(CultureInfo.InvariantCulture);

        public long Size => FileInfo.Length;

        public long Length => FileInfo.Length;

        public string Name => FileInfo.Name;

        public DateTime LastWriteTimeUtc => FileInfo.LastWriteTimeUtc;

        public WopiFile(string filePath, string fileIdentifier)
		{
			FilePath = filePath;
			Identifier = fileIdentifier;
		}

		public Stream GetReadStream()
		{
			return FileInfo.OpenRead();
		}

		public Stream GetWriteStream()
		{
			return FileInfo.Open(FileMode.Truncate);
		}

        public string Owner => FileInfo.GetAccessControl().GetOwner(typeof(NTAccount)).ToString();
    }
}
