using System;
using System.IO;
using WopiHost.Contracts;

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

        override public byte[] GetFileContent()
        {
            MemoryStream ms = new MemoryStream();
            lock (File)
            {
                using (var stream = File.ReadStream)
                {
                    stream.CopyTo(ms);
                }
            }
            return ms.ToArray();
        }

        override public void Save(byte[] newContent)
        {
            lock (File)
            {
                using (var stream = File.WriteStream)
                {
                    stream.Write(newContent, 0, newContent.Length);
                }
            }
            LastUpdated = DateTime.Now;
        }
    }
}
