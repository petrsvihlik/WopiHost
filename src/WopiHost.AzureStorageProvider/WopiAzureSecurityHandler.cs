using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using WopiHost.Abstractions;

namespace WopiHost.AzureStorageProvider;

/// <summary>
/// Security handler for Azure Storage Provider.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WopiAzureSecurityHandler"/> class.
/// </remarks>
/// <param name="logger">Logger instance</param>
public class WopiAzureSecurityHandler(ILogger<WopiAzureSecurityHandler> logger) : IWopiSecurityHandler
{
    private readonly ILogger<WopiAzureSecurityHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public Task<SecurityToken> GenerateAccessToken(string userId, string resourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic token generation - can be enhanced based on requirements
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("your-secret-key-here"));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, userId),
                    new Claim("resource_id", resourceId),
                    new Claim(ClaimTypes.Role, "Reader"),
                    new Claim(ClaimTypes.Role, "Editor")
                ]),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = credentials
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            _logger.LogDebug("Generated access token for user {UserId} and resource {ResourceId}", userId, resourceId);
            return Task.FromResult<SecurityToken>(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating access token for user {UserId} and resource {ResourceId}", userId, resourceId);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<ClaimsPrincipal?> GetPrincipal(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Token is null or empty");
                return Task.FromResult<ClaimsPrincipal?>(null);
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            
            if (!tokenHandler.CanReadToken(token))
            {
                _logger.LogWarning("Cannot read token");
                return Task.FromResult<ClaimsPrincipal?>(null);
            }

            var jwtToken = tokenHandler.ReadJwtToken(token);
            var claims = jwtToken.Claims.ToList();

            // Add additional claims if needed
            claims.Add(new Claim(ClaimTypes.Name, jwtToken.Subject ?? "Unknown"));
            claims.Add(new Claim(ClaimTypes.AuthenticationMethod, "JWT"));

            var identity = new ClaimsIdentity(claims, "JWT");
            var principal = new ClaimsPrincipal(identity);

            _logger.LogDebug("Successfully created principal for user {UserId}", jwtToken.Subject);
            return Task.FromResult<ClaimsPrincipal?>(principal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating principal from token");
            return Task.FromResult<ClaimsPrincipal?>(null);
        }
    }

    /// <inheritdoc/>
    public Task<bool> IsAuthorized(ClaimsPrincipal principal, IWopiAuthorizationRequirement requirement, CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic authorization - can be extended based on requirements
            if (principal?.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("Principal is not authenticated");
                return Task.FromResult(false);
            }

            // Check if user has required permissions
            var hasPermission = CheckPermissions(principal, requirement);
            
            if (!hasPermission)
            {
                _logger.LogWarning("User {UserId} does not have required permissions for requirement {Requirement}", 
                    principal.Identity.Name, requirement.GetType().Name);
            }

            return Task.FromResult(hasPermission);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authorization for user {UserId} and requirement {Requirement}", 
                principal?.Identity?.Name, requirement?.GetType().Name);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public string WriteToken(SecurityToken token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing token");
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public Task<WopiUserPermissions> GetUserPermissions(ClaimsPrincipal principal, IWopiFile file, CancellationToken cancellationToken = default)
    {
        try
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("Principal is not authenticated");
                return Task.FromResult(WopiUserPermissions.None);
            }

            var permissions = WopiUserPermissions.None;

            // Check for read permissions
            if (principal.HasClaim(ClaimTypes.Role, "Reader") || 
                principal.HasClaim(ClaimTypes.Role, "Editor") ||
                principal.HasClaim(ClaimTypes.Role, "Admin"))
            {
                permissions |= WopiUserPermissions.UserCanWrite;
            }

            // Check for write permissions
            if (principal.HasClaim(ClaimTypes.Role, "Editor") ||
                principal.HasClaim(ClaimTypes.Role, "Admin"))
            {
                permissions |= WopiUserPermissions.UserCanWrite;
            }

            // Check for rename permissions
            if (principal.HasClaim(ClaimTypes.Role, "Editor") ||
                principal.HasClaim(ClaimTypes.Role, "Admin"))
            {
                permissions |= WopiUserPermissions.UserCanRename;
            }

            _logger.LogDebug("Retrieved permissions {Permissions} for user {UserId} on file {FileId}", 
                permissions, principal.Identity.Name, file.Identifier);
            return Task.FromResult(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user permissions for user {UserId} on file {FileId}", 
                principal?.Identity?.Name, file?.Identifier);
            return Task.FromResult(WopiUserPermissions.None);
        }
    }

    /// <summary>
    /// Checks if the user has the required permissions for the authorization requirement.
    /// </summary>
    /// <param name="user">The user principal</param>
    /// <param name="requirement">The authorization requirement</param>
    /// <returns>True if user has required permissions</returns>
    private static bool CheckPermissions(ClaimsPrincipal user, IWopiAuthorizationRequirement requirement)
    {
        // Basic permission checking - can be enhanced based on requirements
        // This is a simplified implementation that checks for basic roles
        return user.HasClaim(ClaimTypes.Role, "Reader") || 
               user.HasClaim(ClaimTypes.Role, "Editor") ||
               user.HasClaim(ClaimTypes.Role, "Admin");
    }
}
