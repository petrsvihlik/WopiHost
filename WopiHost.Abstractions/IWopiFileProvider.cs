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
		/// Returns a concrete instance of an implementation of the <see cref="IWopiItem"/>.
		/// </summary>
		/// <param name="identifier">Generic string identifier of a container (typically some kind of a path).</param>
		/// <returns>Instance of a container.</returns>
		IWopiItem GetWopiContainer(string identifier = "");

		/// <summary>
		/// Returns all files from the given source.
		/// This method is very likely to change in the future.
		/// </summary>
		/// <param name="identifier">Container identifier (use null for root)</param>
		List<IWopiFile> GetWopiFiles(string identifier = "");

		/// <summary>
		/// Returns all containers from the given source.
		/// This method is very likely to change in the future.
		/// </summary>
		/// <param name="identifier">Container identifier (use null for root)</param>
		List<IWopiItem> GetWopiContainers(string identifier = "");
	}
}