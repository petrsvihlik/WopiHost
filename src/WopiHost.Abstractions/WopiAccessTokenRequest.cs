namespace WopiHost.Abstractions;

/// <summary>
/// Inputs for issuing a WOPI access token via <see cref="IWopiAccessTokenService.IssueAsync(WopiAccessTokenRequest, System.Threading.CancellationToken)"/>.
/// </summary>
/// <remarks>
/// A WOPI access token is a per-(user, resource, window) bearer credential. The host computes the
/// permissions the user has on the resource (typically via <see cref="IWopiPermissionProvider"/>)
/// and bakes them into the token at issuance. The WOPI client treats the token as opaque bytes
/// and replays it on every <c>/wopi/*</c> call.
/// </remarks>
public class WopiAccessTokenRequest
{
    /// <summary>
    /// Stable identifier of the user the token is issued to.
    /// Written to the principal as <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Optional friendly display name for the user (e.g. "Alice Example").
    /// </summary>
    public string? UserDisplayName { get; init; }

    /// <summary>
    /// Optional email for the user. Written to the principal as <see cref="System.Security.Claims.ClaimTypes.Email"/>.
    /// </summary>
    public string? UserEmail { get; init; }

    /// <summary>
    /// Identifier of the resource (file or container) the token authorizes access to.
    /// Bound to the token via the <see cref="WopiClaimTypes.ResourceId"/> claim.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// Type of the resource the token is bound to.
    /// </summary>
    public required WopiResourceType ResourceType { get; init; }

    /// <summary>
    /// File-level permissions granted at issuance time.
    /// Ignored when <see cref="ResourceType"/> is <see cref="WopiResourceType.Container"/>.
    /// </summary>
    public WopiFilePermissions FilePermissions { get; init; }

    /// <summary>
    /// Container-level permissions granted at issuance time.
    /// Ignored when <see cref="ResourceType"/> is <see cref="WopiResourceType.File"/>.
    /// </summary>
    public WopiContainerPermissions ContainerPermissions { get; init; }

    /// <summary>
    /// Optional override for token lifetime. If <c>null</c>, the issuer's configured default is used.
    /// </summary>
    public TimeSpan? Lifetime { get; init; }

    /// <summary>
    /// Optional additional claims to embed in the token (e.g. tenant id, session id).
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalClaims { get; init; }
}
