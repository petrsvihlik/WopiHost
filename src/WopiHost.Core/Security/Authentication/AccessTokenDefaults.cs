namespace WopiHost.Core.Security.Authentication;

	/// <summary>
	/// Provides the default values for access-token authentication.
	/// </summary>
	public static class AccessTokenDefaults
	{
		/// <summary>
		/// The default authentication scheme name for <see cref="AccessTokenHandler"/>.
		/// </summary>
		public const string AuthenticationScheme = "AccessToken";

		/// <summary>
		/// The query string parameter name that carries the access token.
		/// </summary>
		public const string AccessTokenQueryName = "access_token";
	}