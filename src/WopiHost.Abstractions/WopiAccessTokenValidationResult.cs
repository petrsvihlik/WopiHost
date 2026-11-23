using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Represents the outcome of <see cref="IWopiAccessTokenService.ValidateAsync(string, System.Threading.CancellationToken)"/>.
/// </summary>
/// <param name="IsValid">A value indicating whether the token validated successfully.</param>
/// <param name="Principal">
/// Principal reconstructed from the token's claims, including
/// <see cref="WopiClaimTypes.ResourceId"/>, <see cref="WopiClaimTypes.FilePermissions"/>,
/// and <see cref="WopiClaimTypes.ContainerPermissions"/>. Non-null only when
/// <see cref="IsValid"/> is <see langword="true"/>.
/// </param>
/// <param name="FailureReason">
/// Human-readable reason the token failed to validate (expired, signature mismatch, malformed).
/// Non-null only when <see cref="IsValid"/> is <see langword="false"/>.
/// </param>
public record WopiAccessTokenValidationResult(bool IsValid, ClaimsPrincipal? Principal, string? FailureReason)
{
    /// <summary>Creates a successful validation result.</summary>
    /// <param name="principal">The principal reconstructed from the token's claims.</param>
    /// <returns>A result whose <see cref="IsValid"/> is <see langword="true"/> and whose <see cref="Principal"/> is <paramref name="principal"/>.</returns>
    public static WopiAccessTokenValidationResult Success(ClaimsPrincipal principal) => new(true, principal, null);

    /// <summary>Creates a failed validation result.</summary>
    /// <param name="reason">A human-readable reason the token failed to validate.</param>
    /// <returns>A result whose <see cref="IsValid"/> is <see langword="false"/> and whose <see cref="FailureReason"/> is <paramref name="reason"/>.</returns>
    public static WopiAccessTokenValidationResult Failure(string reason) => new(false, null, reason);
}
