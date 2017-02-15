using Microsoft.AspNetCore.Builder;
using WopiHost.Abstractions;

namespace WopiHost.Core.Security.Authentication
{
	public class AccessTokenAuthenticationOptions : AuthenticationOptions
	{
		/// <summary>
		/// Defines whether the token should be stored in the
		/// <see cref="Http.Authentication.AuthenticationProperties"/> after a successful authorization.
		/// </summary>
		public bool SaveToken { get; set; } = true;


		public IWopiSecurityHandler SecurityHandler { get; set; }


		public AccessTokenAuthenticationOptions()
		{

			AuthenticationScheme = AccessTokenDefaults.AuthenticationScheme;
			AutomaticAuthenticate = true;
			AutomaticChallenge = true;
		}
	}
}
