namespace WopiHost.Abstractions;

/// <summary>
/// Provides concrete instances of IWopiFiles.
/// </summary>
public interface IWopiStorageProvider
	{
		/// <summary>
		/// Returns a concrete instance of an implementation of the <see cref="IWopiFile"/>.
		/// </summary>
		/// <param name="identifier">Generic string identifier of a file (typically some kind of a path).</param>
		/// <returns>Instance of a file.</returns>
		IWopiFile GetWopiFile(string identifier);

		/// <summary>
		/// Returns a concrete instance of an implementation of the <see cref="IWopiFolder"/>.
		/// </summary>
		/// <param name="identifier">Generic string identifier of a container (typically some kind of a path).</param>
		/// <returns>Instance of a container.</returns>
		IWopiFolder GetWopiContainer(string identifier = "");

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
		List<IWopiFolder> GetWopiContainers(string identifier = "");

		/// <summary>
		/// Reference to the root container.
		/// </summary>
		IWopiFolder RootContainerPointer { get; }
	}