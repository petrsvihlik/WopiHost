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
    /// <param name="userId">User identifier</param>
    /// <param name="resourceId">Identifier of a resource</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authorization token</returns>
    Task<SecurityToken> GenerateAccessToken(string userId, string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a principal object using the given token. The principal can be extracted directly from the token or retrieved from an external storage based on the user identifier contained within the token.
    /// </summary>
    /// <param name="token">Token to use for retrieval of the principal</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Principal object extracted from the token</returns>
    Task<ClaimsPrincipal?> GetPrincipal(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies whether the given principal is authorized to perform a given operation on the given resource.
    /// </summary>
    /// <param name="principal">User principal object</param>
    /// <param name="requirement">Type of operation to be performed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TRUE if the given principal is authorized to perform a given operation on the given resource.</returns>
    Task<bool> IsAuthorized(ClaimsPrincipal principal, IWopiAuthorizationRequirement requirement, CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns a string representation of a <see cref="SecurityToken"/>
	/// </summary>
	string WriteToken(SecurityToken token);

    /// <summary>
    /// Retrieves permissions for the given user on the given file.
    /// </summary>
    /// <param name="file">the file in question</param>
    /// <param name="principal">User principal object</param>
	/// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    Task<WopiUserPermissions> GetUserPermissions(ClaimsPrincipal principal, IWopiFile file, CancellationToken cancellationToken = default);
}