using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Outcome of <see cref="IWopiAccessTokenService.ValidateAsync(string, System.Threading.CancellationToken)"/>.
/// </summary>
/// <param name="IsValid">Whether the token validated successfully.</param>
/// <param name="Principal">
/// Principal reconstructed from the token's claims, including
/// <see cref="WopiClaimTypes.ResourceId"/>, <see cref="WopiClaimTypes.FilePermissions"/>,
/// and <see cref="WopiClaimTypes.ContainerPermissions"/>. Non-null only when
/// <see cref="IsValid"/> is <c>true</c>.
/// </param>
/// <param name="FailureReason">
/// Human-readable reason the token failed to validate (expired, signature mismatch, malformed).
/// Non-null only when <see cref="IsValid"/> is <c>false</c>.
/// </param>
public record WopiAccessTokenValidationResult(bool IsValid, ClaimsPrincipal? Principal, string? FailureReason)
{
    /// <summary>Successful validation with the resulting principal.</summary>
    public static WopiAccessTokenValidationResult Success(ClaimsPrincipal principal) => new(true, principal, null);

    /// <summary>Failed validation with a human-readable reason.</summary>
    public static WopiAccessTokenValidationResult Failure(string reason) => new(false, null, reason);
}
