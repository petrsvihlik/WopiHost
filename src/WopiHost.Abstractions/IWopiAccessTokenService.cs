namespace WopiHost.Abstractions;

/// <summary>
/// Issues and validates WOPI access tokens.
/// </summary>
/// <remarks>
/// <para>
/// A WOPI access token is a host-issued bearer credential scoped to a single
/// <c>(user, resource)</c> pair for a bounded time window. The WOPI client treats the token
/// as opaque bytes and replays it on every <c>/wopi/*</c> call. The host validates it on each
/// request and re-materializes the user as a <see cref="System.Security.Claims.ClaimsPrincipal"/>.
/// </para>
/// <para>
/// The default implementation registered by <c>AddWopi()</c> uses a signed JWT with a
/// configurable signing key (see <c>WopiSecurityOptions</c>). Replace it via DI to use opaque
/// reference tokens, an external token service, or any other token format. Permission claims
/// baked into the token (see <see cref="WopiClaimTypes"/>) are read by the authorization
/// pipeline to enforce per-resource access.
/// </para>
/// </remarks>
public interface IWopiAccessTokenService
{
    /// <summary>
    /// Issues a new access token for a specific user and resource.
    /// </summary>
    /// <remarks>
    /// Callers (typically the host's URL-generating code) compute the permissions to grant —
    /// usually via <see cref="IWopiPermissionProvider"/> — and pass them in <paramref name="request"/>.
    /// Those permissions are baked into the token's claims.
    /// </remarks>
    /// <param name="request">User, resource, and permissions to bind into the token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WopiAccessToken> IssueAsync(WopiAccessTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an access token and returns the principal it represents.
    /// </summary>
    /// <param name="token">The token string as received from the WOPI client (typically the <c>access_token</c> query value).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WopiAccessTokenValidationResult> ValidateAsync(string token, CancellationToken cancellationToken = default);
}
