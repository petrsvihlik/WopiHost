using System;
using System.IO;
using WopiHost.Core.Models;

namespace WopiHost.Core
{
    public interface IEditSession
    {
        /// <summary>
        /// Session identifier.
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// Last update time of the file.
        /// </summary>
        DateTime LastUpdated { get; }

        /// <summary>
        /// E-mail of the current user.
        /// </summary>
        string Email { get; }

        /// <summary>
        /// Returns content of a file as a byte array. Used if <see cref="AbstractEditSession.GetFileStream"/> is not implemented.
        /// </summary>
        /// <returns></returns>
        byte[] GetFileContent();

        /// <summary>
        /// Returns a stream of file content. Used primarily to get content of a file. Alternatively, implement <see cref="AbstractEditSession.GetFileContent"/>.
        /// </summary>
        Stream GetFileStream();

        /// <summary>
        /// Disposes of all allocated resources.
        /// </summary>
        void Dispose();

        /// <summary>
        /// Accepts new content of a file and replaces old content with it. 
        /// </summary>
        /// <param name="newContent">Content to set</param>
        /// <returns>Gives an opportunity of returning a response to this action (returns null if not applicable)</returns>
        Action<Stream> SetFileContent(byte[] newContent);

        /// <summary>
        /// Gets information about a file.
        /// </summary>
        /// <returns>Object with attributes according to the specification (https://msdn.microsoft.com/en-us/library/hh622920.aspx)</returns>
        CheckFileInfo GetCheckFileInfo();
    }
}