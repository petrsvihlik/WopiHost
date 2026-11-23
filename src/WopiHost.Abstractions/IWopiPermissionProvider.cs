using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Computes what a user is allowed to do with a file or container.
/// </summary>
/// <remarks>
/// <para>
/// This is the primary extensibility point for plugging a host's ACL model into WOPI. The default
/// implementation reads permissions from the principal's <see cref="WopiClaimTypes.FilePermissions"/>
/// / <see cref="WopiClaimTypes.ContainerPermissions"/> claims (which are populated from the access
/// token), falling back to the configured defaults from <c>WopiHostOptions</c> when no claims
/// are present.
/// </para>
/// <para>
/// Replace via <c>services.AddSingleton&lt;IWopiPermissionProvider, MyAclProvider&gt;()</c>
/// (registered <i>after</i> <c>AddWopi()</c>) to consult a custom ACL store. The provider is
/// called:
/// </para>
/// <list type="bullet">
///   <item><description>at <i>token issuance</i> by host code that builds WOPI URLs, to decide what permissions to bake into the token; and</description></item>
///   <item><description>during <c>CheckFileInfo</c> / <c>CheckContainerInfo</c> to populate the <c>UserCan*</c> response flags.</description></item>
/// </list>
/// </remarks>
public interface IWopiPermissionProvider
{
    /// <summary>
    /// Gets the permissions <paramref name="user"/> has on <paramref name="file"/>.
    /// </summary>
    /// <param name="user">The principal whose permissions to resolve.</param>
    /// <param name="file">The file the permissions apply to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The permissions granted on the file.</returns>
    Task<WopiFilePermissions> GetFilePermissionsAsync(ClaimsPrincipal user, IWopiFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the permissions <paramref name="user"/> has on <paramref name="container"/>.
    /// </summary>
    /// <param name="user">The principal whose permissions to resolve.</param>
    /// <param name="container">The container the permissions apply to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The permissions granted on the container.</returns>
    Task<WopiContainerPermissions> GetContainerPermissionsAsync(ClaimsPrincipal user, IWopiContainer container, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether <paramref name="user"/> is authorized to overwrite the existing target
    /// <paramref name="existingFile"/> via <c>PutRelativeFile</c> when the client sets
    /// <c>X-WOPI-OverwriteRelativeTarget: true</c> against a name that already exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/putrelativefile">PutRelativeFile spec</see>
    /// mandates a distinct status code for this failure mode:
    /// </para>
    /// <para>
    /// <i>If the user is not authorized to overwrite the target file, the host must respond with
    /// a <c>501 Not Implemented</c>.</i>
    /// </para>
    /// <para>
    /// Unlike <see cref="GetFilePermissionsAsync"/>, this decision is made per existing target
    /// file at the moment of the call: the target's identity isn't known when the access token
    /// is minted, so it can't be encoded as a <see cref="WopiFilePermissions"/> flag ahead of
    /// time. Hosts that distinguish "may create relative files" from "may overwrite this specific
    /// file" express the latter here.
    /// </para>
    /// <para>
    /// The default implementation returns <see langword="true"/>, so any user that passed the
    /// <see cref="Permission.Create"/> gate may overwrite any existing target. Hosts that need
    /// stricter rules (owner-only overwrite, label-based gating) override it and consult their
    /// ACL store using <paramref name="existingFile"/>'s identity.
    /// </para>
    /// </remarks>
    /// <param name="user">The authenticated principal making the request.</param>
    /// <param name="existingFile">The target file the user is attempting to overwrite.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the overwrite is authorized; <see langword="false"/>
    /// to surface the spec-mandated <c>501 Not Implemented</c>.</returns>
    Task<bool> CanOverwriteFileAsync(ClaimsPrincipal user, IWopiFile existingFile, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
