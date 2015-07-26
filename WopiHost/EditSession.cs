using System;
using System.Security.Cryptography;
using WopiHost.Contracts;
using WopiHost.Models;

namespace WopiHost
{
	public abstract class EditSession
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

		protected EditSession(IWopiFile file, string sessionId, string login, string name, string email, bool isAnonymous)
		{
			//TODO: work with users
			File = file;
			SessionId = sessionId;
			Name = name;
			Login = login;
			Email = email;
			IsAnonymous = isAnonymous;
		}
		public abstract byte[] GetFileContent();

		public virtual void Dispose() { }

		//TODO: consolidate the 2 saving methods
		public virtual void SetFileContent(byte[] new_content) { }
		public virtual void Save() { }

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
			cfi.OwnerId = Login;
			cfi.UserFriendlyName = Name;
			cfi.Version = File.LastWriteTimeUtc.ToString("s");
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
