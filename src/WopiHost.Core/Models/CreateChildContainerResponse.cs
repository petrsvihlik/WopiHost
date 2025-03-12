using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Models;

/// <summary>
/// Response to the <see cref="Controllers.ContainersController.CreateChildContainer(string, UtfString?, UtfString?, CancellationToken)"/> method.
/// </summary>
/// <param name="ContainerPointer">container name and URL</param>
/// <param name="ContainerInfo">optional CheckContainerInfo</param>
public record CreateChildContainerResponse(ChildContainer ContainerPointer, WopiCheckContainerInfo? ContainerInfo = null);
