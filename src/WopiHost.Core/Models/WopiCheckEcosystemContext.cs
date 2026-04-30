using System.Security.Claims;
using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Context for the CheckEcosystem operation.
/// </summary>
/// <param name="User">the current user.</param>
/// <param name="CheckEcosystem">the default created <see cref="WopiCheckEcosystem"/></param>
public record WopiCheckEcosystemContext(ClaimsPrincipal? User, WopiCheckEcosystem CheckEcosystem);
