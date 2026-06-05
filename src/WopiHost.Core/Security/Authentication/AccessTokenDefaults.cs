namespace WopiHost.Core.Security.Authentication;

	/// <summary>
	/// Default values and constants related to the access token authentication.
	/// </summary>
	public static class AccessTokenDefaults
	{
		/// <summary>
		/// Default authentication scheme name for <see cref="AccessTokenAuthenticationOptions"/>.
		/// </summary>
		public const string AuthenticationScheme = "AccessToken";

		/// <summary>
		/// Default query string name used for the access token.
		/// </summary>
		public const string AccessTokenQueryName = "access_token";
	}