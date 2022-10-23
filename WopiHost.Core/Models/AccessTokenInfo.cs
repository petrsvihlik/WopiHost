namespace WopiHost.Core.Models;

	/// <summary>
	/// Represents a WOPI access token.
	/// </summary>
	public class AccessTokenInfo
	{
		/// <summary>
		/// A string access token for the file specified in the X-WOPI-WopiSrc request header.
		/// </summary>
		public string AccessToken { get; set; }

		/// <summary>
		/// A long value representing the time that the access token provided in the response will expire. 
		/// See access_token_ttl for more information on how this value is defined.
		/// </summary>
		public long AccessTokenExpiry { get; set; }
	}
