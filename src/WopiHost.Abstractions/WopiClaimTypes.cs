namespace WopiHost.Abstractions;

/// <summary>
/// Claim types used by the WOPI access-token pipeline.
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
    /// Identifier of the resource the access token was issued for. Used for audit/logging;
    /// the default authorization pipeline does not enforce a strict binding between this
    /// claim and the route's resource id, because WOPI clients use a single token across
    /// related resources (file → ancestor container → siblings). Layer a custom
    /// <see cref="Microsoft.AspNetCore.Authorization.IAuthorizationHandler"/> if your scenario
    /// requires strict per-resource binding.
    /// </summary>
    public const string ResourceId = "wopi:rid";

    /// <summary>
    /// Type of the bound resource (<see cref="WopiResourceType.File"/> or <see cref="WopiResourceType.Container"/>).
    /// </summary>
    public const string ResourceType = "wopi:rtype";

    /// <summary>
    /// Comma-separated <see cref="WopiFilePermissions"/> flags granted at issuance.
    /// Only meaningful when <see cref="ResourceType"/> is <see cref="WopiResourceType.File"/>.
    /// </summary>
    public const string FilePermissions = "wopi:fperms";

    /// <summary>
    /// Comma-separated <see cref="WopiContainerPermissions"/> flags granted at issuance.
    /// Only meaningful when <see cref="ResourceType"/> is <see cref="WopiResourceType.Container"/>.
    /// </summary>
    public const string ContainerPermissions = "wopi:cperms";

    /// <summary>
    /// Friendly display name of the user (mirrors <see cref="System.Security.Claims.ClaimTypes.Name"/>).
    /// </summary>
    public const string UserDisplayName = "wopi:uname";
}
