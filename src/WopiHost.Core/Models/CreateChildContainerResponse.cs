using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Response to the <see cref="Controllers.ContainersController.CreateChildContainer(string, string?, string?, CancellationToken)"/> method.
/// </summary>
/// <param name="ContainerPointer">container name and URL</param>
/// <param name="ContainerInfo">optional CheckContainerInfo</param>
public record CreateChildContainerResponse(ChildContainer ContainerPointer, WopiCheckContainerInfo? ContainerInfo = null);
