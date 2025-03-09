using System.Security.Claims;
using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Context for the CheckContainerInfo operation.
/// </summary>
/// <param name="User">the current user.</param>
/// <param name="Container">the current container resource.</param>
/// <param name="CheckContainerInfo">the default created <see cref="WopiCheckContainerInfo"/></param>
public record WopiCheckContainerInfoContext(ClaimsPrincipal? User, IWopiFolder Container, WopiCheckContainerInfo CheckContainerInfo);