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
    Task<WopiContainerPermissions> GetContainerPermissionsAsync(ClaimsPrincipal user, IWopiContainer container, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decides whether <paramref name="user"/> is authorized to overwrite the existing target
    /// <paramref name="existingFile"/> via <c>PutRelativeFile</c> when the client sets
    /// <c>X-WOPI-OverwriteRelativeTarget: true</c> against a name that already exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">PutRelativeFile spec</see>
    /// mandates a distinct status code for this failure mode:
    /// </para>
    /// <para>
    /// "If the user is not authorized to overwrite the target file, the host must respond with
    /// a <c>501 Not Implemented</c>."
    /// </para>
    /// <para>
    /// Unlike <see cref="GetFilePermissionsAsync"/>, this decision is made <em>per existing
    /// target file</em> at the moment of the call — the file's identity isn't known when the
    /// access token is minted, so the decision can't be encoded as a flag on
    /// <see cref="WopiFilePermissions"/> ahead of time. This seam exists precisely so hosts
    /// that distinguish "user may create relative files in this container" from "user may
    /// overwrite this specific file" can express the latter at the right moment.
    /// </para>
    /// <para>
    /// The default implementation returns <see langword="true"/> to preserve the historical
    /// behaviour (any user that passed the <c>Permission.Create</c> gate may overwrite any
    /// existing target). Hosts that need stricter rules — owner-only overwrite, label-based
    /// gating, etc. — override and consult their ACL store using the supplied
    /// <paramref name="existingFile"/>'s identity.
    /// </para>
    /// </remarks>
    /// <param name="user">The authenticated principal making the request.</param>
    /// <param name="existingFile">The target file the user is attempting to overwrite.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns><see langword="true"/> if the overwrite is authorized; <see langword="false"/>
    /// to surface the spec-mandated <c>501 Not Implemented</c>.</returns>
    Task<bool> CanOverwriteFileAsync(ClaimsPrincipal user, IWopiFile existingFile, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
