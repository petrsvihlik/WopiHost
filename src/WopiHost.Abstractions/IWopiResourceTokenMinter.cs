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
/// token bound to the *new* resource id rather than reusing the inbound token. The default
/// <see cref="IWopiAccessTokenService"/> binds tokens to a single resource via the
/// <see cref="WopiAccessTokenRequest.ResourceId"/> claim; reusing an inbound token across
/// resources would either fail downstream authorization or open a token-trading hole per
/// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/security#preventing-token-trading">
/// the WOPI security guidance</see>.
/// </para>
/// <para>
/// This seam wraps <see cref="IWopiAccessTokenService.IssueAsync"/> with the permission-lookup
/// step (via <see cref="IWopiPermissionProvider"/>) and the claim plumbing (FindFirst for the
/// user's id / name / email) so endpoints don't duplicate the boilerplate. A host that wants a
/// custom token-minting policy (e.g. opaque
/// revocable tokens, an external token-issuance service, or an extra audit step before issuing)
/// can replace the default registration; the WOPI endpoints will pick up the override.
/// </para>
/// <para>
/// Architecturally also a workaround for an Infer# precision loss: routing the per-resource
/// token mint through an injected interface (this) instead of a static helper means the await
/// at the call site lands on an injected dependency, which the analyzer tracks cleanly. See
/// the historical notes in <c>.github/infer-allowlist.txt</c> and #471 for the full story.
/// </para>
/// </remarks>
public interface IWopiResourceTokenMinter
{
    /// <summary>
    /// Resolves <paramref name="user"/>'s permissions for <paramref name="file"/> via
    /// <see cref="IWopiPermissionProvider"/> and issues a file-scoped access token carrying
    /// them. Returns the full <see cref="WopiAccessToken"/> (token string + expiry); most
    /// callers only need the <see cref="WopiAccessToken.Token"/>, but the
    /// <see cref="WopiAccessToken.ExpiresAt"/> is surfaced for callers that need to bake the
    /// expiry into the response (e.g. the bootstrapper's <c>GET_NEW_ACCESS_TOKEN</c>).
    /// </summary>
    Task<WopiAccessToken> MintForFileAsync(ClaimsPrincipal user, IWopiFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves <paramref name="user"/>'s permissions for <paramref name="container"/> via
    /// <see cref="IWopiPermissionProvider"/> and issues a container-scoped access token
    /// carrying them.
    /// </summary>
    Task<WopiAccessToken> MintForContainerAsync(ClaimsPrincipal user, IWopiContainer container, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a minimum-privilege (no <c>FilePermissions</c> / <c>ContainerPermissions</c>)
    /// token bound to <paramref name="resourceId"/> + <paramref name="resourceType"/>. Used by
    /// the <c>ecosystem_pointer</c> handlers, which return a URL to <c>/wopi/ecosystem</c> and
    /// don't need any resource-mutation capability baked in.
    /// </summary>
    Task<WopiAccessToken> MintMinimumPrivilegeAsync(ClaimsPrincipal user, string resourceId, WopiResourceType resourceType, CancellationToken cancellationToken = default);
}
