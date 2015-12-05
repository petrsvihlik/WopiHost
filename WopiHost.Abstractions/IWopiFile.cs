using System;
using System.IO;

namespace WopiHost.Abstractions
{
	/// <summary>
	/// Representation of a file.
	/// </summary>
	public interface IWopiFile : IWopiItem
	{
		/// <summary>
		/// Indicates whether the file already exists.
		/// </summary>
		bool Exists { get; }

		/// <summary>
		/// Gets size of the file in bytes.
		/// </summary>
		long Length { get; }

		/// <summary>
		/// Time of the last modification of the file.
		/// </summary>
		DateTime LastWriteTimeUtc { get; }

		/// <summary>
		/// Extension without the initial dot.
		/// </summary>
		string Extension { get; }

		/// <summary>
		/// Gets read-only stream.
		/// </summary>
		Stream GetReadStream();

		/// <summary>
		/// Gets r/w stream.
		/// </summary>
		Stream GetWriteStream();
	}
}
