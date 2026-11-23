using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Represents the context passed to <see cref="IWopiHostExtensions.OnCheckEcosystemAsync"/> for the CheckEcosystem operation.
/// </summary>
/// <param name="User">The current user, if any.</param>
/// <param name="CheckEcosystem">The <see cref="WopiCheckEcosystem"/> response built by the host.</param>
public record WopiCheckEcosystemContext(ClaimsPrincipal? User, WopiCheckEcosystem CheckEcosystem);
