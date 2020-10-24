namespace WopiHost.Core.Security.Authentication
{
	public static class AccessTokenDefaults
	{
		/// <summary>
		/// Default value for AuthenticationScheme property in the AccessTokenAuthenticationOptions
		/// </summary>
		public const string AUTHENTICATION_SCHEME = "AccessToken";

		/// <summary>
		/// Default query string name used for the access token.
		/// </summary>
		public const string ACCESS_TOKEN_QUERY_NAME = "access_token";
	}
}