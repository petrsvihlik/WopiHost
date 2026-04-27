using System.Security.Claims;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Models;

namespace WopiHost.Core.Security.Authorization;

/// <summary>
/// Default <see cref="IWopiPermissionProvider"/>: returns permissions from the principal's
/// claims when the user is authenticated via a WOPI access token (the common request-time path),
/// and falls back to the configured <see cref="WopiHostOptions.DefaultFilePermissions"/>
/// / <see cref="WopiHostOptions.DefaultContainerPermissions"/> otherwise (the typical
/// pre-issuance path).
/// </summary>
/// <remarks>
/// Replace via DI to plug in your own ACL backend:
/// <code>
/// services.AddSingleton&lt;IWopiPermissionProvider, MyAclProvider&gt;();
/// </code>
/// </remarks>
public class DefaultWopiPermissionProvider(IOptionsMonitor<WopiHostOptions> options) : IWopiPermissionProvider
{
    /// <inheritdoc/>
    public Task<WopiFilePermissions> GetFilePermissionsAsync(ClaimsPrincipal user, IWopiFile file, CancellationToken cancellationToken = default)
    {
        var claim = user.FindFirst(WopiClaimTypes.FilePermissions)?.Value;
        if (!string.IsNullOrEmpty(claim) && Enum.TryParse<WopiFilePermissions>(claim, ignoreCase: true, out var fromClaim))
        {
            return Task.FromResult(fromClaim);
        }
        return Task.FromResult(options.CurrentValue.DefaultFilePermissions);
    }

    /// <inheritdoc/>
    public Task<WopiContainerPermissions> GetContainerPermissionsAsync(ClaimsPrincipal user, IWopiFolder container, CancellationToken cancellationToken = default)
    {
        var claim = user.FindFirst(WopiClaimTypes.ContainerPermissions)?.Value;
        if (!string.IsNullOrEmpty(claim) && Enum.TryParse<WopiContainerPermissions>(claim, ignoreCase: true, out var fromClaim))
        {
            return Task.FromResult(fromClaim);
        }
        return Task.FromResult(options.CurrentValue.DefaultContainerPermissions);
    }
}
