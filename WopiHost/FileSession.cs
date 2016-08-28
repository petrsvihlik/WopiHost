using System;
using System.IO;
using WopiHost.Abstractions;

namespace WopiHost
{
	public class FileSession : EditSession
	{
		public FileSession(IWopiFile file, string sessionId, string login = "Anonymous", string name = "Anonymous", string email = "", bool isAnonymous = true)
			: base(file, sessionId, login, name, email, isAnonymous)
		{ }

		public override bool IsCobaltSession
		{
			get { return false; }
		}

		public override byte[] GetFileContent()
		{
			MemoryStream ms = new MemoryStream();
			lock (File)
			{
				using (var stream = File.GetReadStream())
				{
					stream.CopyTo(ms);
				}
			}
			return ms.ToArray();
		}

		public override Action<Stream> SetFileContent(byte[] newContent)
		{
			lock (File)
			{
				using (var stream = File.GetWriteStream())
				{
					stream.Write(newContent, 0, newContent.Length);
				}
			}
			LastUpdated = DateTime.Now;

			// No response
			return null;
		}
	}
}
