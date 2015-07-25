using System;
using System.Security.Cryptography;
using WopiHost.Contracts;
using WopiHost.Models;

namespace WopiHost
{
    abstract public class EditSession
    {
        protected string FileIdentifier
        {
            get { return File.Identifier; }
        }

        public IWopiFile File { get; }

        public virtual bool IsCobaltSession { get; }

        public string SessionId
        {
            get; private set;
        }

        public string Login
        {
            get; private set;
        }

        public string Name
        {
            get; private set;
        }

        public string Email
        {
            get; private set;
        }

        public bool IsAnonymous
        {
            get; private set;
        }

        public DateTime LastUpdated
        {
            get; set;
        }
        public long FileLength
        {
            get { return File.Length; }
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
        public virtual void Save(byte[] new_content) { }
        public virtual void Save() { }

        public virtual CheckFileInfo GetCheckFileInfo()
        {
            CheckFileInfo cfi = new CheckFileInfo();
            string sha256 = "";

            using (var sha = SHA256.Create())
            {
                using (var stream = File.ReadStream)
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
                if (File.Exists)
                {
                    cfi.Size = File.Length;
                }
                else
                {
                    cfi.Size = 0;
                }
            }
            return cfi;
        }
    }
}
