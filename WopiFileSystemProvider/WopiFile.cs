using System;
using System.IO;
using WopiHost.Contracts;

namespace WopiFileSystemProvider
{
	public class WopiFile : IWopiFile
	{
		protected FileInfo fileInfo;
		protected string FilePath { get; set; }

		protected FileInfo FileInfo
		{
			get { return fileInfo ?? (fileInfo = new FileInfo(FilePath)); }
		}

		public WopiFile(string filePath, string fileIdentifier)
		{
			FilePath = filePath;
		    Identifier = fileIdentifier;
		}

	    public string Identifier { get; private set; }
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
        
		public Stream ReadStream
		{
			get { return FileInfo.OpenRead(); }
		}

		public Stream WriteStream
		{
			get { return FileInfo.Open(FileMode.Truncate); } 
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
	}
}
