using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Computes what a user is allowed to do with a file or container.
/// </summary>
/// <remarks>
/// <para>
/// This is the primary extensibility point for plugging your ACL model into WOPI. The default
/// implementation reads permissions from the principal's <see cref="WopiClaimTypes.FilePermissions"/>
/// / <see cref="WopiClaimTypes.ContainerPermissions"/> claims (which are populated from the access
/// token), falling back to the configured defaults from <c>WopiHostOptions</c> when no claims
/// are present.
/// </para>
/// <para>
/// Replace via <c>services.AddSingleton&lt;IWopiPermissionProvider, MyAclProvider&gt;()</c>
/// (registered <em>after</em> <c>AddWopi()</c>) to consult your own ACL store. The provider is
/// called both:
/// </para>
/// <list type="bullet">
///   <item><description>at <em>token issuance</em> by host code that builds WOPI URLs, to decide what permissions to bake into the token; and</description></item>
///   <item><description>during <c>CheckFileInfo</c> / <c>CheckContainerInfo</c> to populate the <c>UserCan*</c> response flags.</description></item>
/// </list>
/// </remarks>
public interface IWopiPermissionProvider
{
    /// <summary>
    /// Returns the permissions <paramref name="user"/> has on <paramref name="file"/>.
    /// </summary>
    Task<WopiFilePermissions> GetFilePermissionsAsync(ClaimsPrincipal user, IWopiFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the permissions <paramref name="user"/> has on <paramref name="container"/>.
    /// </summary>
    Task<WopiContainerPermissions> GetContainerPermissionsAsync(ClaimsPrincipal user, IWopiFolder container, CancellationToken cancellationToken = default);
}
