namespace WopiHost.Abstractions;

/// <summary>
/// Provides the claim types used by the WOPI access-token pipeline.
/// </summary>
/// <remarks>
/// These claims are written into the WOPI access token at issuance time and read back
/// out of the <see cref="System.Security.Claims.ClaimsPrincipal"/> by the authorization
/// pipeline. They bind a token to a specific resource and capture the permissions the
/// user had when the token was issued.
/// </remarks>
public static class WopiClaimTypes
{
    /// <summary>
    /// The claim type carrying the identifier of the resource the access token was issued for.
    /// </summary>
    /// <remarks>
    /// Used for audit/logging only: the default authorization pipeline does not bind this claim
    /// to the route's resource id, because WOPI clients use a single token across related
    /// resources (file → ancestor container → siblings). Hosts that need strict per-resource
    /// binding can add a custom <see cref="Microsoft.AspNetCore.Authorization.IAuthorizationHandler"/>.
    /// </remarks>
    public const string ResourceId = "wopi:rid";

    /// <summary>
    /// The claim type carrying the type of the bound resource (<see cref="WopiResourceType.File"/> or <see cref="WopiResourceType.Container"/>).
    /// </summary>
    public const string ResourceType = "wopi:rtype";

    /// <summary>
    /// The claim type carrying the comma-separated <see cref="WopiFilePermissions"/> flags granted at issuance.
    /// Read by authorization on file endpoints, whatever the token's <see cref="ResourceType"/>.
    /// </summary>
    public const string FilePermissions = "wopi:fperms";

    /// <summary>
    /// The claim type carrying the comma-separated <see cref="WopiContainerPermissions"/> flags granted at issuance.
    /// Read by authorization on container endpoints, whatever the token's <see cref="ResourceType"/>.
    /// </summary>
    public const string ContainerPermissions = "wopi:cperms";

    /// <summary>
    /// The claim type carrying the friendly display name of the user (mirrors <see cref="System.Security.Claims.ClaimTypes.Name"/>).
    /// </summary>
    public const string UserDisplayName = "wopi:uname";
}
