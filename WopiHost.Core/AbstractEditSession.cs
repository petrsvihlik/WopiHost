using System;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using WopiHost.Abstractions;
using WopiHost.Core.Models;

namespace WopiHost.Core
{
    public abstract class AbstractEditSession : IEditSession
    {
        private readonly SHA256 SHA = SHA256.Create();

        protected IWopiFile File { get; }

        protected readonly CheckFileInfo CheckFileInfo = new CheckFileInfo();

        public string SessionId
        {
            get;
        }

        public DateTime LastUpdated
        {
            get; protected set;
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
            else
            {
                CheckFileInfo.IsAnonymousUser = true;
            }

            CheckFileInfo.OwnerId = File.Owner;

            //TODO: set up capabilities dynamically & implement them http://wopi.readthedocs.io/projects/wopirest/en/latest/files/CheckFileInfo.html
            CheckFileInfo.SupportsCoauth = true;
            CheckFileInfo.SupportsFolders = true;
            CheckFileInfo.SupportsLocks = true;
            CheckFileInfo.SupportsGetLock = false;
            CheckFileInfo.SupportsExtendedLockLength = true;
            CheckFileInfo.SupportsEcosystem = false;
            CheckFileInfo.SupportsGetFileWopiSrc = false;
            CheckFileInfo.SupportedShareUrlTypes = false;
            CheckFileInfo.SupportsScenarioLinks = false;
            CheckFileInfo.SupportsSecureStore = false;
            CheckFileInfo.SupportsUpdate = true;
            CheckFileInfo.SupportsCobalt = true;
            CheckFileInfo.SupportsRename = false;
            CheckFileInfo.SupportsDeleteFile = false;
            CheckFileInfo.SupportsUserInfo = false;
        }

        public abstract byte[] GetFileContent();

        public abstract Stream GetFileStream();

        public virtual void Dispose() { }

        public abstract Action<Stream> SetFileContent(byte[] newContent);

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
