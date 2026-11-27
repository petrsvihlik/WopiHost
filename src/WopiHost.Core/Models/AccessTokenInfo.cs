namespace WopiHost.Core.Models;

/// <summary>
/// Represents a WOPI access token.
/// </summary>
public class AccessTokenInfo
{
	/// <summary>
	/// Gets or sets the access token for the resource specified in the <c>X-WOPI-WopiSrc</c> request header.
	/// </summary>
	public required string AccessToken { get; set; }

	/// <summary>
	/// Gets or sets the time at which <see cref="AccessToken"/> expires, as milliseconds since
	/// 1970-01-01T00:00:00Z, matching the WOPI <c>access_token_ttl</c> parameter.
	/// </summary>
	public long AccessTokenExpiry { get; set; }
}
