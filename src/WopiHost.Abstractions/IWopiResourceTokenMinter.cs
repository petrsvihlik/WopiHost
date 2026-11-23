using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Mints fresh resource-scoped WOPI access tokens for inclusion in response URLs (child links,
/// ancestor links, ecosystem pointers).
/// </summary>
/// <remarks>
/// <para>
/// Every endpoint that surfaces a child or ancestor URL — <c>EnumerateAncestors</c>,
/// <c>EnumerateChildren</c>, <c>PutRelativeFile</c>, <c>CreateChildFile</c>,
/// <c>CreateChildContainer</c>, the <c>ecosystem_pointer</c> handlers — must mint a fresh
/// token bound to the <em>new</em> resource id rather than reusing the inbound token. The default
/// <see cref="IWopiAccessTokenService"/> binds tokens to a single resource via the
/// <see cref="WopiAccessTokenRequest.ResourceId"/> claim; reusing an inbound token across
/// resources would either fail downstream authorization or open a token-trading hole per
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/security#preventing-token-trading">
/// the WOPI security guidance</see>.
/// </para>
/// <para>
/// This seam wraps <see cref="IWopiAccessTokenService.IssueAsync"/> with the permission lookup
/// (via <see cref="IWopiPermissionProvider"/>) and the claim plumbing (the user's id, name, and
/// email). A host that wants a custom minting policy (opaque revocable tokens, an external
/// token-issuance service, an extra audit step before issuing) replaces the default
/// registration; the WOPI endpoints pick up the override.
/// </para>
/// </remarks>
public interface IWopiResourceTokenMinter
{
    /// <summary>
    /// Resolves <paramref name="user"/>'s permissions for <paramref name="file"/> via
    /// <see cref="IWopiPermissionProvider"/> and issues a file-scoped access token carrying them.
    /// </summary>
    /// <param name="user">The principal to issue the token for.</param>
    /// <param name="file">The file the token is bound to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The issued token and its expiry. Most callers need only
    /// <see cref="WopiAccessToken.Token"/>; <see cref="WopiAccessToken.ExpiresAt"/> serves callers
    /// that report the expiry in the response (for example the bootstrapper's
    /// <c>GET_NEW_ACCESS_TOKEN</c>).</returns>
    Task<WopiAccessToken> MintForFileAsync(ClaimsPrincipal user, IWopiFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves <paramref name="user"/>'s permissions for <paramref name="container"/> via
    /// <see cref="IWopiPermissionProvider"/> and issues a container-scoped access token
    /// carrying them.
    /// </summary>
    /// <param name="user">The principal to issue the token for.</param>
    /// <param name="container">The container the token is bound to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The issued token and its expiry.</returns>
    Task<WopiAccessToken> MintForContainerAsync(ClaimsPrincipal user, IWopiContainer container, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a minimum-privilege token (no file or container permissions) bound to
    /// <paramref name="resourceId"/> and <paramref name="resourceType"/>, for the
    /// <c>ecosystem_pointer</c> URLs returned by the file- and container-side
    /// <c>GetEcosystem</c> operations.
    /// </summary>
    /// <remarks>
    /// The ecosystem endpoint needs no resource-mutation capability, so none is baked in. The
    /// bootstrapper's ecosystem token does not go through this method: it binds to the root
    /// container and resolves the user id from <c>NameIdentifier</c> with a <c>Upn</c> fallback,
    /// whereas this method requires <c>NameIdentifier</c>.
    /// </remarks>
    /// <param name="user">The principal to issue the token for.</param>
    /// <param name="resourceId">The identifier of the resource the token is bound to.</param>
    /// <param name="resourceType">The type of the resource identified by <paramref name="resourceId"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The issued token and its expiry.</returns>
    Task<WopiAccessToken> MintForEcosystemAsync(ClaimsPrincipal user, string resourceId, WopiResourceType resourceType, CancellationToken cancellationToken = default);
}
