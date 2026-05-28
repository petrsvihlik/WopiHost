using System.Security.Claims;
using WopiHost.Abstractions;
using WopiHost.Web.Oidc.Infrastructure;
using Xunit;

namespace WopiHost.IntegrationTests;

/// <summary>
/// Tests <see cref="OidcRolePermissionMapper.Resolve"/> across the claim shapes that real IdPs
/// emit (single role, multiple roles, missing roles, alternate role-claim type, case variation).
/// Pure-unit; no Docker / Testcontainers required.
/// </summary>
public class OidcRolePermissionMapperTests
{
    private static ClaimsPrincipal Principal(string roleClaimType, params string[] roleValues)
    {
        var identity = new ClaimsIdentity("test");
        foreach (var role in roleValues)
        {
            identity.AddClaim(new Claim(roleClaimType, role));
        }
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void EditorRole_GrantsWriteRenameAttendPresent()
    {
        var permissions = OidcRolePermissionMapper.Resolve(
            Principal("roles", OidcRolePermissionMapper.EditorRole), "roles");

        Assert.True(permissions.HasFlag(WopiFilePermissions.UserCanWrite));
        Assert.True(permissions.HasFlag(WopiFilePermissions.UserCanRename));
        Assert.True(permissions.HasFlag(WopiFilePermissions.UserCanAttend));
        Assert.True(permissions.HasFlag(WopiFilePermissions.UserCanPresent));
    }

    [Fact]
    public void ViewerRole_GrantsAttendOnly()
    {
        var permissions = OidcRolePermissionMapper.Resolve(
            Principal("roles", OidcRolePermissionMapper.ViewerRole), "roles");

        Assert.True(permissions.HasFlag(WopiFilePermissions.UserCanAttend));
        Assert.False(permissions.HasFlag(WopiFilePermissions.UserCanWrite));
        Assert.False(permissions.HasFlag(WopiFilePermissions.UserCanRename));
    }

    [Fact]
    public void NoRoles_ReturnsNone()
    {
        var permissions = OidcRolePermissionMapper.Resolve(Principal("roles"), "roles");
        Assert.Equal(WopiFilePermissions.None, permissions);
    }

    [Fact]
    public void UnknownRole_ReturnsNone()
    {
        var permissions = OidcRolePermissionMapper.Resolve(
            Principal("roles", "some.other.role"), "roles");
        Assert.Equal(WopiFilePermissions.None, permissions);
    }

    [Fact]
    public void EditorAndViewer_PrefersEditor()
    {
        var permissions = OidcRolePermissionMapper.Resolve(
            Principal("roles", OidcRolePermissionMapper.ViewerRole, OidcRolePermissionMapper.EditorRole),
            "roles");

        Assert.True(permissions.HasFlag(WopiFilePermissions.UserCanWrite));
    }

    [Fact]
    public void RoleMatching_IsCaseInsensitive()
    {
        var permissions = OidcRolePermissionMapper.Resolve(
            Principal("roles", "WOPI.EDITOR"), "roles");

        Assert.True(permissions.HasFlag(WopiFilePermissions.UserCanWrite));
    }

    [Fact]
    public void RoleClaimType_IsConfigurable()
    {
        // Some IdPs (e.g. Auth0) emit roles in a namespaced claim — verify the mapper honors the configured name.
        var permissions = OidcRolePermissionMapper.Resolve(
            Principal("https://example.com/roles", OidcRolePermissionMapper.EditorRole),
            "https://example.com/roles");

        Assert.True(permissions.HasFlag(WopiFilePermissions.UserCanWrite));
    }

    [Fact]
    public void NullPrincipal_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => OidcRolePermissionMapper.Resolve(null!, "roles"));
    }

    [Fact]
    public void EmptyRoleClaimType_Throws()
    {
        Assert.Throws<ArgumentException>(() => OidcRolePermissionMapper.Resolve(Principal("roles"), ""));
    }
}
