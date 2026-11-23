using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Represents the context passed to <see cref="IWopiHostExtensions.OnCheckContainerInfoAsync"/> for the CheckContainerInfo operation.
/// </summary>
/// <param name="User">The current user, if any.</param>
/// <param name="Container">The container the response describes.</param>
/// <param name="CheckContainerInfo">The <see cref="WopiCheckContainerInfo"/> populated by the default builder.</param>
public record WopiCheckContainerInfoContext(ClaimsPrincipal? User, IWopiContainer Container, WopiCheckContainerInfo CheckContainerInfo);
