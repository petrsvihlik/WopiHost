using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace WopiHost.Abstractions;

	/// <summary>
	/// Performs security-related actions.
	/// </summary>
	public interface IWopiSecurityHandler
	{
	    /// <summary>
	    /// Generates authorization token for the given combination of user and resource.
	    /// </summary>
	    /// <returns>Authorization token</returns>
	    SecurityToken GenerateAccessToken(string user, string resourceId);

		/// <summary>
		/// Retrieves a principal object using the given token. The principal can be extracted directly from the token or retrieved from an external storage based on the user identifier contained within the token.
		/// </summary>
		/// <param name="token">Token to use for retrieval of the principal</param>
		/// <returns>Principal object extracted from the token</returns>
		ClaimsPrincipal GetPrincipal(string token);

		/// <summary>
		/// Verifies whether the given principal is authorized to perform a given operation on the given resource.
		/// </summary>
		/// <param name="principal">User principal object</param>
		/// <param name="resourceId">Identifier of a resource</param>
		/// <param name="operation">Type of operation to be performed</param>
		/// <returns>TRUE if the given principal is authorized to perform a given operation on the given resource.</returns>
		bool IsAuthorized(ClaimsPrincipal principal, string resourceId, WopiAuthorizationRequirement operation);

	    /// <summary>
	    /// Returns a string representation of a <see cref="SecurityToken"/>
	    /// </summary>
	    string WriteToken(SecurityToken token);
}
