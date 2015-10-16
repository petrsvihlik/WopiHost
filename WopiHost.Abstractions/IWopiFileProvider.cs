using System.Collections.Generic;

namespace WopiHost.Abstractions
{
	/// <summary>
	/// Provides concrete instances of IWopiFiles.
	/// </summary>
	public interface IWopiFileProvider
	{
		/// <summary>
		/// Returns a concrete instance of an implementation of the <see cref="IWopiFile"/>.
		/// </summary>
		/// <param name="identifier">Generic string identifier of a file (typically some kind of a path).</param>
		/// <returns>Instance of a file.</returns>
		IWopiFile GetWopiFile(string identifier);

		/// <summary>
		/// Returns all files from the given source.
		/// This method is very likely to change in the future.
		/// </summary>
		List<IWopiFile> GetWopiFiles();
	}
}