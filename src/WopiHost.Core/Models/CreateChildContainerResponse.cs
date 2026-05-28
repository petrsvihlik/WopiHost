using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Response body for the <c>CreateChildContainer</c> endpoint
/// (<c>POST /wopi/containers/{id}</c> with <c>X-WOPI-Override: CREATE_CHILD_CONTAINER</c>).
/// </summary>
/// <param name="ContainerPointer">container name and URL</param>
/// <param name="ContainerInfo">optional CheckContainerInfo</param>
public record CreateChildContainerResponse(ChildContainer ContainerPointer, WopiCheckContainerInfo? ContainerInfo = null);
