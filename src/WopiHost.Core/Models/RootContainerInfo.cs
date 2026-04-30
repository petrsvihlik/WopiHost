using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Object describing the root container.
/// Spec: <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getrootcontainer#required-response-properties"/>.
/// </summary>
public class RootContainerInfo
{
	/// <summary>
	/// Object pointing to a container.
	/// </summary>
	public required ChildContainer ContainerPointer { get; set; }

	/// <summary>
	/// Optional CheckContainerInfo data for the root container. Including this saves the WOPI
	/// client an extra round-trip to <see cref="WopiCheckContainerInfo"/> and is strongly
	/// recommended by the WOPI spec.
	/// </summary>
	public WopiCheckContainerInfo? ContainerInfo { get; set; }
}
