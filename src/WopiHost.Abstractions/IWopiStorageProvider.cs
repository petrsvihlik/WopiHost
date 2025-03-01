using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Provides concrete instances of IWopiFiles.
/// </summary>
public interface IWopiStorageProvider
{
    /// <summary>
    /// Optional update of WopiCheckFileInfo
    /// </summary>
    /// <param name="file"></param>	
	/// <param name="hostCapabilities"></param>
    /// <param name="wopiCheckFileInfo"></param>
	/// <param name="principal"></param>
    /// <returns>WopiCheckFileInfo object</returns>
	/// <remarks>you can either:
	/// 1. return null - original <see cref="WopiCheckFileInfo"/> will be used as-is
	/// 1. return original <see cref="WopiCheckFileInfo"/> as-is
	/// 2. return an updated <see cref="WopiCheckFileInfo"/>
	/// 3. return your own descendent from <see cref="WopiCheckFileInfo"/> with added custom properties
	/// </remarks>
    WopiCheckFileInfo? GetWopiCheckFileInfo(
        IWopiFile file,
		WopiHostCapabilities hostCapabilities,
		ClaimsPrincipal? principal,
        WopiCheckFileInfo wopiCheckFileInfo);

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