using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Represents the response body of the <c>CreateChildContainer</c> operation
/// (<c>POST /wopi/containers/{id}</c> with <c>X-WOPI-Override: CREATE_CHILD_CONTAINER</c>).
/// </summary>
/// <param name="ContainerPointer">The name and URL of the created container.</param>
/// <param name="ContainerInfo">The optional <c>CheckContainerInfo</c> data of the created container.</param>
public record CreateChildContainerResponse(ChildContainer ContainerPointer, WopiCheckContainerInfo? ContainerInfo = null);
