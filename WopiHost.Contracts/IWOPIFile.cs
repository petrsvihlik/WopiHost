using System;
using System.IO;

namespace WopiHost.Contracts
{
	public interface IWopiFile
	{
		bool Exists { get; }

		Stream ReadStream { get; }

		Stream WriteStream { get; }

		long Length { get; }

		string Name { get; }

		DateTime LastWriteTimeUtc { get; }

        /// <summary>
        /// Extension without the initial dot.
        /// </summary>
	    string Extension { get; }
        string Identifier { get; }
	}
}
