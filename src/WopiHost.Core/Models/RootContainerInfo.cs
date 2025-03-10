﻿using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Object describing the root container.
/// Implemented in accordance with https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getrootcontainer#required-response-properties
/// </summary>
public class RootContainerInfo
{
	/// <summary>
	/// Object pointing to a container.
	/// </summary>
	public required ChildContainer ContainerPointer { get; set; }

	//TODO: initialize according to https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getrootcontainer
	/// <summary>
	/// Hosts can optionally include the ContainerInfo property, which should match the CheckContainerInfo response for the root container.
	///	If not provided, the WOPI client will call CheckContainerInfo to retrieve it.Including this property in the response is strongly recommended so that the WOPI client does not need to make an additional call to CheckContainerInfo.
	/// </summary>
	public WopiCheckContainerInfo? ContainerInfo { get; set; }
}
