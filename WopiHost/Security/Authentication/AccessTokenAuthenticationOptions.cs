using Microsoft.AspNetCore.Builder;

namespace WopiHost.Security
{
	public class AccessTokenAuthenticationOptions : AuthenticationOptions
	{
		/// <summary>
		/// Defines whether the token should be stored in the
		/// <see cref="Http.Authentication.AuthenticationProperties"/> after a successful authorization.
		/// </summary>
		public bool SaveToken { get; set; } = true;

		public AccessTokenAuthenticationOptions()
		{

			AuthenticationScheme = AccessTokenDefaults.AuthenticationScheme;
			AutomaticAuthenticate = true;
			AutomaticChallenge = true;
		}
	}
}
