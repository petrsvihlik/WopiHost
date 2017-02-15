namespace WopiHost.Core.Security.Authentication
{
	public static class AccessTokenDefaults
	{
		/// <summary>
		/// Default value for AuthenticationScheme property in the AccessTokenAuthenticationOptions
		/// </summary>
		public const string AuthenticationScheme = "AccessToken";

		/// <summary>
		/// Default query string name used for the access token.
		/// </summary>
		public const string AccessTokenQueryName = "access_token";
	}
}