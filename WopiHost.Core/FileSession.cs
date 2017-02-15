using System;
using System.IO;
using System.Security.Claims;
using WopiHost.Abstractions;

namespace WopiHost.Core
{
	public class FileSession : AbstractEditSession
	{
		public FileSession(IWopiFile file, string sessionId, ClaimsPrincipal principal)
			: base(file, sessionId, principal)
		{ }
		
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
