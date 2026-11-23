using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Represents the root container in a <c>GetRootContainer</c> response.
/// </summary>
/// <seealso href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getrootcontainer#required-response-properties">GetRootContainer response properties</seealso>
public class RootContainerInfo
{
	/// <summary>
	/// Gets or sets the pointer to the root container.
	/// </summary>
	public required ChildContainer ContainerPointer { get; set; }

	/// <summary>
	/// Gets or sets the optional <c>CheckContainerInfo</c> data of the root container. Including it saves the WOPI
	/// client a <c>CheckContainerInfo</c> round trip and is strongly recommended by the WOPI spec.
	/// </summary>
	public WopiCheckContainerInfo? ContainerInfo { get; set; }
}
