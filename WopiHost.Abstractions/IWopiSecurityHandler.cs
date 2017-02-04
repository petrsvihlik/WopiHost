using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace WopiHost.Abstractions
{
	/// <summary>
	/// Performs security-related actions.
	/// </summary>
	public interface IWopiSecurityHandler
	{
		/// <summary>
		/// Generates authorization token for the given value.
		/// </summary>
		/// <param name="value"></param>
		/// <returns>Authorization token</returns>
		string GenerateAccessToken(string value);

		ClaimsPrincipal GetPrincipal(string token);


		bool IsAuthorized(ClaimsPrincipal principal, string resource, IAuthorizationRequirement operation);
	}
}
