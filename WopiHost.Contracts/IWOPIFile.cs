using System;
using System.IO;

namespace WopiHost.Contracts
{
	/// <summary>
	/// Representation of a file.
	/// </summary>
	public interface IWopiFile
	{
		/// <summary>
		/// Unique identifier of the file.
		/// </summary>
		string Identifier { get; }

		/// <summary>
		/// Name of the file (for conclusive identification see the <see cref="Identifier"/>)
		/// </summary>
		string Name { get; }

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
