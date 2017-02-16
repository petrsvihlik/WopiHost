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
		
		protected FileInfo fileInfo;

		protected FileVersionInfo fileVersionInfo;

		protected string FilePath { get; set; }

		protected FileInfo FileInfo
		{
			get { return fileInfo ?? (fileInfo = new FileInfo(FilePath)); }
		}

		protected FileVersionInfo FileVersionInfo
		{
			get { return fileVersionInfo ?? (fileVersionInfo = FileVersionInfo.GetVersionInfo(FilePath)); }
		}

		/// <inheritdoc />
		public bool Exists
		{
			get
			{
				return FileInfo.Exists;
			}
		}

		public string Extension
		{
			get
			{
				var ext = FileInfo.Extension;
				if (ext.StartsWith("."))
				{
					ext = ext.Substring(1);
				}
				return ext;
			}
		}

		public string Version
		{
			get { return FileVersionInfo.FileVersion ?? FileInfo.LastWriteTimeUtc.ToString(CultureInfo.InvariantCulture); }
		}

		public long Size
		{
			get { return FileInfo.Length; }
		}

		public long Length
		{
			get
			{
				return FileInfo.Length;
			}
		}

		public string Name
		{
			get { return FileInfo.Name; }
		}

		public DateTime LastWriteTimeUtc
		{
			get { return FileInfo.LastWriteTimeUtc; }
		}

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

		public string Owner
		{
			get
			{
				return FileInfo.GetAccessControl().GetOwner(typeof(NTAccount)).ToString();
			}
		}
	}
}
