namespace WopiHost.Abstractions;

/// <summary>
/// Represents the inputs for issuing a WOPI access token via <see cref="IWopiAccessTokenService.IssueAsync(WopiAccessTokenRequest, System.Threading.CancellationToken)"/>.
/// </summary>
/// <remarks>
/// A WOPI access token is a per-(user, resource, window) bearer credential. The host computes the
/// permissions the user has on the resource (typically via <see cref="IWopiPermissionProvider"/>)
/// and bakes them into the token at issuance. The WOPI client treats the token as opaque bytes
/// and replays it on every <c>/wopi/*</c> call.
/// </remarks>
public record WopiAccessTokenRequest
{
    /// <summary>
    /// Gets the stable identifier of the user the token is issued to.
    /// Written to the principal as <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Gets the optional friendly display name of the user (e.g. "Alice Example").
    /// </summary>
    public string? UserDisplayName { get; init; }

    /// <summary>
    /// Gets the optional email address of the user. Written to the principal as <see cref="System.Security.Claims.ClaimTypes.Email"/>.
    /// </summary>
    public string? UserEmail { get; init; }

    /// <summary>
    /// Gets the identifier of the resource (file or container) the token authorizes access to.
    /// Bound to the token via the <see cref="WopiClaimTypes.ResourceId"/> claim.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// Gets the type of the resource the token is bound to.
    /// </summary>
    public required WopiResourceType ResourceType { get; init; }

    /// <summary>
    /// Gets the file-level permissions granted at issuance time.
    /// Written to the token whenever not <see cref="WopiFilePermissions.None"/>, whatever the
    /// <see cref="ResourceType"/>.
    /// </summary>
    public WopiFilePermissions FilePermissions { get; init; }

    /// <summary>
    /// Gets the container-level permissions granted at issuance time.
    /// Written to the token whenever not <see cref="WopiContainerPermissions.None"/>, whatever the
    /// <see cref="ResourceType"/>.
    /// </summary>
    public WopiContainerPermissions ContainerPermissions { get; init; }

    /// <summary>
    /// Gets the optional token lifetime override. If <see langword="null"/>, the issuer's configured default is used.
    /// </summary>
    public TimeSpan? Lifetime { get; init; }

    /// <summary>
    /// Gets the optional additional claims to embed in the token (e.g. tenant id, session id).
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalClaims { get; init; }
}
