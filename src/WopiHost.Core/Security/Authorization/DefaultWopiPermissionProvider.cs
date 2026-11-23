using System.Security.Claims;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Models;

namespace WopiHost.Core.Security.Authorization;

/// <summary>
/// Provides the default <see cref="IWopiPermissionProvider"/>, which reads permissions from the
/// principal's WOPI permission claims.
/// </summary>
/// <param name="options">The monitor for the host options that supply the fallback permissions.</param>
/// <remarks>
/// <para>
/// Claims are present when the user is authenticated via a WOPI access token (the common
/// request-time path). Without a parseable claim (the typical pre-issuance path), the provider falls
/// back to <see cref="WopiHostOptions.DefaultFilePermissions"/> /
/// <see cref="WopiHostOptions.DefaultContainerPermissions"/>.
/// </para>
/// <para>
/// Hosts with their own ACL backend replace it via DI.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddSingleton&lt;IWopiPermissionProvider, MyAclProvider&gt;();
/// </code>
/// </example>
public class DefaultWopiPermissionProvider(IOptionsMonitor<WopiHostOptions> options) : IWopiPermissionProvider
{
    /// <inheritdoc/>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    public Task<WopiFilePermissions> GetFilePermissionsAsync(ClaimsPrincipal user, IWopiFile file, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var claim = user.FindFirst(WopiClaimTypes.FilePermissions)?.Value;
        if (!string.IsNullOrEmpty(claim) && Enum.TryParse<WopiFilePermissions>(claim, ignoreCase: true, out var fromClaim))
        {
            return Task.FromResult(fromClaim);
        }
        return Task.FromResult(options.CurrentValue.DefaultFilePermissions);
    }

    /// <inheritdoc/>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    public Task<WopiContainerPermissions> GetContainerPermissionsAsync(ClaimsPrincipal user, IWopiContainer container, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var claim = user.FindFirst(WopiClaimTypes.ContainerPermissions)?.Value;
        if (!string.IsNullOrEmpty(claim) && Enum.TryParse<WopiContainerPermissions>(claim, ignoreCase: true, out var fromClaim))
        {
            return Task.FromResult(fromClaim);
        }
        return Task.FromResult(options.CurrentValue.DefaultContainerPermissions);
    }
}
