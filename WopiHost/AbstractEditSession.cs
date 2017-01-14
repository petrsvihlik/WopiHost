using System;
using System.IO;
using System.Security.Cryptography;
using WopiHost.Abstractions;
using WopiHost.Models;

namespace WopiHost
{
	public abstract class AbstractEditSession
	{
		protected IWopiFile File { get; }

		public abstract bool IsCobaltSession { get; }

		public string SessionId
		{
			get;
		}

		public string Login
		{
			get;
		}

		public string Name
		{
			get;
		}

		public string Email
		{
			get;
		}

		public bool IsAnonymous
		{
			get;
		}

		public DateTime LastUpdated
		{
			get; set;
		}

		protected AbstractEditSession(IWopiFile file, string sessionId, string login, string name, string email, bool isAnonymous)
		{
			//TODO: work with users
			File = file;
			SessionId = sessionId;
			Name = name;
			Login = login;
			Email = email;
			IsAnonymous = isAnonymous;
		}

		/// <summary>
		/// Returns content of a file.
		/// </summary>
		/// <returns></returns>
		public abstract byte[] GetFileContent();

		/// <summary>
		/// Disposes of all allocated resources.
		/// </summary>
		public virtual void Dispose() { }

		/// <summary>
		/// Accepts new content of a file and replaces old content with it. 
		/// </summary>
		/// <param name="newContent">Content to set</param>
		/// <returns>Gives an opportunity of returning a response to this action (returns null if not applicable)</returns>
		public abstract Action<Stream> SetFileContent(byte[] newContent);

		/// <summary>
		/// Gets information about a file.
		/// </summary>
		/// <returns>Object with attributes according to the specification (https://msdn.microsoft.com/en-us/library/hh622920.aspx)</returns>
		public virtual CheckFileInfo GetCheckFileInfo()
		{
			CheckFileInfo cfi = new CheckFileInfo();
			string sha256 = "";

			using (var sha = SHA256.Create())
			{
				using (var stream = File.GetReadStream())
				{
					byte[] checksum = sha.ComputeHash(stream);
					sha256 = Convert.ToBase64String(checksum);
				}
			}
			cfi.SHA256 = sha256;
			cfi.BaseFileName = File.Name;
			cfi.FileExtension = "." + File.Extension.TrimStart('.');
			cfi.OwnerId = Login;
			cfi.UserFriendlyName = Name;
			cfi.Version = File.LastWriteTimeUtc.ToString("s");
			cfi.LastModifiedTime = File.LastWriteTimeUtc.ToString("o");
			cfi.SupportsCoauth = true;
			cfi.SupportsFolders = true;
			cfi.SupportsLocks = true;
			cfi.SupportsScenarioLinks = false;
			cfi.SupportsSecureStore = false;
			cfi.SupportsUpdate = true;
			cfi.UserCanWrite = true;
			cfi.SupportsCobalt = IsCobaltSession;

			lock (File)
			{
				cfi.Size = File.Exists ? File.Length : 0;
			}
			return cfi;
		}
	}
}
