using Microsoft.AspNetCore.Authentication;

namespace WopiHost.Core.Security.Authentication;

	/// <summary>
	/// Default values and constants related to the access token authentication.
	/// </summary>
	public static class AccessTokenDefaults
	{
		/// <summary>
		/// Default value for <see cref="AuthenticationScheme"/> property in the <see cref="AccessTokenAuthenticationOptions"/>
		/// </summary>
		public const string AUTHENTICATION_SCHEME = "AccessToken";

		/// <summary>
		/// Default query string name used for the access token.
		/// </summary>
		public const string ACCESS_TOKEN_QUERY_NAME = "access_token";
	}