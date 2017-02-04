using System;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using WopiHost.Abstractions;
using WopiHost.Models;

namespace WopiHost
{
	public abstract class AbstractEditSession
	{
		private readonly SHA256 SHA = SHA256.Create();

		protected IWopiFile File { get; }

		public readonly CheckFileInfo CheckFileInfo = new CheckFileInfo();

		public string SessionId
		{
			get;
		}

		public DateTime LastUpdated
		{
			get; set;
		}

		public string Email { get; private set; }

		private string FileHash
		{
			get
			{
				using (var stream = File.GetReadStream())
				{
					byte[] checksum = SHA.ComputeHash(stream);
					return Convert.ToBase64String(checksum);
				}
			}
		}

		protected AbstractEditSession(IWopiFile file, string sessionId, ClaimsPrincipal principal)
		{
			File = file;
			SessionId = sessionId;

			if (principal != null)
			{
				CheckFileInfo.UserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;


				CheckFileInfo.OwnerId = CheckFileInfo.UserId; //TODO: Init properly
				CheckFileInfo.UserFriendlyName = principal.FindFirst(ClaimTypes.Name)?.Value;
				Email = principal.FindFirst(ClaimTypes.Email)?.Value;

				WopiUserPermissions permissions = (WopiUserPermissions)Enum.Parse(typeof(WopiUserPermissions), principal.FindFirst(WopiClaimTypes.UserPermissions).Value);

				CheckFileInfo.ReadOnly = permissions.HasFlag(WopiUserPermissions.ReadOnly);
				CheckFileInfo.RestrictedWebViewOnly = permissions.HasFlag(WopiUserPermissions.RestrictedWebViewOnly);
				CheckFileInfo.UserCanAttend = permissions.HasFlag(WopiUserPermissions.UserCanAttend);
				CheckFileInfo.UserCanNotWriteRelative = permissions.HasFlag(WopiUserPermissions.UserCanNotWriteRelative);
				CheckFileInfo.UserCanPresent = permissions.HasFlag(WopiUserPermissions.UserCanPresent);
				CheckFileInfo.UserCanRename = permissions.HasFlag(WopiUserPermissions.UserCanRename);
				CheckFileInfo.UserCanWrite = permissions.HasFlag(WopiUserPermissions.UserCanWrite);
				CheckFileInfo.WebEditingDisabled = permissions.HasFlag(WopiUserPermissions.WebEditingDisabled);
			}

			//TODO: set up capabilities dynamically
			CheckFileInfo.SupportsCoauth = true;
			CheckFileInfo.SupportsFolders = true;
			CheckFileInfo.SupportsLocks = true;
			CheckFileInfo.SupportsScenarioLinks = false;
			CheckFileInfo.SupportsSecureStore = false;
			CheckFileInfo.SupportsUpdate = true;
			CheckFileInfo.SupportsCobalt = true;
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
			CheckFileInfo.SHA256 = FileHash;
			CheckFileInfo.BaseFileName = File.Name;
			CheckFileInfo.FileExtension = "." + File.Extension.TrimStart('.');
			CheckFileInfo.Version = File.LastWriteTimeUtc.ToString("s");
			CheckFileInfo.LastModifiedTime = File.LastWriteTimeUtc.ToString("o");

			lock (File)
			{
				CheckFileInfo.Size = File.Exists ? File.Length : 0;
			}
			return CheckFileInfo;
		}
	}
}
