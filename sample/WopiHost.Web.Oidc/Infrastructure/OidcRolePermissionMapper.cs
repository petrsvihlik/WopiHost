using System.Security.Claims;
using WopiHost.Abstractions;

namespace WopiHost.Web.Oidc.Infrastructure;

/// <summary>
/// Maps OIDC role claims to <see cref="WopiFilePermissions"/> at access-token mint time.
/// </summary>
/// <remarks>
/// <para>
/// This is the host-frontend half of WOPI permission resolution: the frontend bakes per-resource
/// permissions into the access token (claim <c>wopi:fperms</c>), and the WOPI backend's
/// <see cref="IWopiPermissionProvider"/> reads them back when answering CheckFileInfo.
/// </para>
/// <para>
/// Production hosts replace this with a database/ACL lookup keyed off the user's <c>sub</c>
/// claim and the resource id. The mapping below is intentionally a tiny, role-driven default
/// chosen to demonstrate the seam — not an authorization model.
/// </para>
/// <para>
/// IdP-agnostic by design: the role-claim type is configurable (Entra emits <c>roles</c>; some
/// providers emit <c>groups</c> or custom claims). The mapper compares case-insensitively and
/// treats unknown roles as no-permissions.
/// </para>
/// </remarks>
public static class OidcRolePermissionMapper
{
    /// <summary>Role string that grants full edit access.</summary>
    public const string EditorRole = "wopi.editor";
    /// <summary>Role string that grants read-only access.</summary>
    public const string ViewerRole = "wopi.viewer";

    private static readonly WopiFilePermissions EditorPermissions =
        WopiFilePermissions.UserCanWrite |
        WopiFilePermissions.UserCanRename |
        WopiFilePermissions.UserCanAttend |
        WopiFilePermissions.UserCanPresent;

    /// <summary>
    /// Resolves <see cref="WopiFilePermissions"/> for a signed-in OIDC user. Returns
    /// <see cref="WopiFilePermissions.None"/> if no recognized role is present (the WOPI client
    /// will then load the document in restricted view-only mode).
    /// </summary>
    /// <param name="user">The signed-in principal from the OIDC cookie.</param>
    /// <param name="roleClaimType">Claim type that carries roles (e.g. <c>roles</c>, <c>groups</c>).</param>
    public static WopiFilePermissions Resolve(ClaimsPrincipal user, string roleClaimType)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(roleClaimType);

        var roles = user.FindAll(roleClaimType).Select(c => c.Value).ToArray();
        if (roles.Length == 0)
        {
            return WopiFilePermissions.None;
        }

        if (roles.Any(r => string.Equals(r, EditorRole, StringComparison.OrdinalIgnoreCase)))
        {
            return EditorPermissions;
        }
        if (roles.Any(r => string.Equals(r, ViewerRole, StringComparison.OrdinalIgnoreCase)))
        {
            // No write/rename — read-only viewer.
            return WopiFilePermissions.UserCanAttend;
        }
        return WopiFilePermissions.None;
    }
}
