namespace WopiHost.Abstractions;

/// <summary>
/// A WOPI access token produced by <see cref="IWopiAccessTokenService.IssueAsync(WopiAccessTokenRequest, System.Threading.CancellationToken)"/>.
/// </summary>
/// <param name="Token">
/// The serialized token. Hosts pass this string to the WOPI client as the
/// <c>access_token</c> query parameter when generating URLs. The token is opaque to the
/// WOPI client.
/// </param>
/// <param name="ExpiresAt">
/// UTC instant at which the token stops being valid. Hosts typically also report this back
/// to the WOPI client as <c>access_token_ttl</c> (Unix milliseconds).
/// </param>
public record WopiAccessToken(string Token, DateTimeOffset ExpiresAt);
