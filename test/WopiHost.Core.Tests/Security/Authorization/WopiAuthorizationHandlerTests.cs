using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Tests.Security.Authorization;

public class WopiAuthorizationHandlerTests
{
    private readonly WopiAuthorizationHandler _handler = new(NullLogger<WopiAuthorizationHandler>.Instance);

    private static AuthorizationHandlerContext BuildContext(
        WopiAuthorizeAttribute requirement,
        string routeId,
        ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["id"] = routeId;
        return new AuthorizationHandlerContext([requirement], principal, httpContext);
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "wopi-test"));

    [Fact]
    public async Task Fails_When_User_Not_Authenticated()
    {
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Read);
        var ctx = BuildContext(requirement, "fileId", new ClaimsPrincipal(new ClaimsIdentity()));

        await _handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Token_Bound_To_Different_Resource_Is_Permitted_By_Default_For_Cross_Resource_Navigation()
    {
        // The default handler logs the mismatch (audit) but allows the call. This matches
        // how Office for the web and the Microsoft WOPI validator use a single token to
        // navigate file → ancestor container → siblings. Hosts that want strict per-resource
        // binding can layer a custom IAuthorizationHandler.
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Read);
        var ctx = BuildContext(requirement, "fileId",
            Authenticated(new Claim(WopiClaimTypes.ResourceId, "other-file")));

        await _handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Succeeds_For_Read_When_Bound_To_Same_File()
    {
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Read);
        var ctx = BuildContext(requirement, "fileId",
            Authenticated(new Claim(WopiClaimTypes.ResourceId, "fileId")));

        await _handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
        // The handler also assigns the route id onto the requirement for downstream consumers.
        Assert.Equal("fileId", requirement.ResourceId);
    }

    [Fact]
    public async Task Fails_For_Update_When_Token_Lacks_UserCanWrite()
    {
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Update);
        var ctx = BuildContext(requirement, "fileId",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "fileId"),
                new Claim(WopiClaimTypes.FilePermissions, WopiFilePermissions.UserCanRename.ToString())));

        await _handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Succeeds_For_Update_When_Token_Has_UserCanWrite()
    {
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Update);
        var ctx = BuildContext(requirement, "fileId",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "fileId"),
                new Claim(WopiClaimTypes.FilePermissions, WopiFilePermissions.UserCanWrite.ToString())));

        await _handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Fails_For_Update_When_Token_Marks_ReadOnly_Even_With_Write()
    {
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Update);
        var perms = WopiFilePermissions.UserCanWrite | WopiFilePermissions.ReadOnly;
        var ctx = BuildContext(requirement, "fileId",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "fileId"),
                new Claim(WopiClaimTypes.FilePermissions, perms.ToString())));

        await _handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Container_Delete_Requires_UserCanDelete_Claim()
    {
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Delete);
        var ctx = BuildContext(requirement, "container-1",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "container-1"),
                new Claim(WopiClaimTypes.ContainerPermissions, WopiContainerPermissions.UserCanDelete.ToString())));

        await _handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Container_Read_Without_Permission_Claim_Still_Succeeds()
    {
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Read);
        var ctx = BuildContext(requirement, "container-1",
            Authenticated(new Claim(WopiClaimTypes.ResourceId, "container-1")));

        await _handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task File_Rename_Requires_UserCanRename_Claim()
    {
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Rename);
        var ctx = BuildContext(requirement, "fileId",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "fileId"),
                new Claim(WopiClaimTypes.FilePermissions, WopiFilePermissions.UserCanRename.ToString())));

        await _handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task File_Delete_Is_Gated_By_UserCanWrite()
    {
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Delete);
        var without = BuildContext(requirement, "fileId",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "fileId"),
                new Claim(WopiClaimTypes.FilePermissions, WopiFilePermissions.UserCanRename.ToString())));
        await _handler.HandleAsync(without);
        Assert.False(without.HasSucceeded);

        var withWrite = BuildContext(new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Delete), "fileId",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "fileId"),
                new Claim(WopiClaimTypes.FilePermissions, WopiFilePermissions.UserCanWrite.ToString())));
        await _handler.HandleAsync(withWrite);
        Assert.True(withWrite.HasSucceeded);
    }

    [Fact]
    public async Task File_Create_Is_Blocked_When_UserCanNotWriteRelative_Set()
    {
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Create);
        var perms = WopiFilePermissions.UserCanWrite | WopiFilePermissions.UserCanNotWriteRelative;
        var ctx = BuildContext(requirement, "fileId",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "fileId"),
                new Claim(WopiClaimTypes.FilePermissions, perms.ToString())));

        await _handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task File_Create_Allowed_When_UserCanNotWriteRelative_Not_Set()
    {
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.CreateChildFile);
        var ctx = BuildContext(requirement, "fileId",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "fileId"),
                new Claim(WopiClaimTypes.FilePermissions, WopiFilePermissions.UserCanWrite.ToString())));

        await _handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Container_Create_And_Rename_Map_To_Specific_Flags()
    {
        // CreateChildContainer
        var ctx1 = BuildContext(
            new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Create),
            "c",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "c"),
                new Claim(WopiClaimTypes.ContainerPermissions, WopiContainerPermissions.UserCanCreateChildContainer.ToString())));
        await _handler.HandleAsync(ctx1);
        Assert.True(ctx1.HasSucceeded);

        // CreateChildFile
        var ctx2 = BuildContext(
            new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.CreateChildFile),
            "c",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "c"),
                new Claim(WopiClaimTypes.ContainerPermissions, WopiContainerPermissions.UserCanCreateChildFile.ToString())));
        await _handler.HandleAsync(ctx2);
        Assert.True(ctx2.HasSucceeded);

        // Rename
        var ctx3 = BuildContext(
            new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Rename),
            "c",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "c"),
                new Claim(WopiClaimTypes.ContainerPermissions, WopiContainerPermissions.UserCanRename.ToString())));
        await _handler.HandleAsync(ctx3);
        Assert.True(ctx3.HasSucceeded);
    }

    [Fact]
    public async Task No_Route_Id_Means_No_Binding_Check_So_Read_Succeeds()
    {
        // Endpoints without an {id} route value (e.g. ecosystem-style) should not
        // be rejected just because the principal has no resource id claim.
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Read);
        var httpContext = new DefaultHttpContext(); // no RouteValues["id"]
        var ctx = new AuthorizationHandlerContext([requirement], Authenticated(), httpContext);

        await _handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Permission_None_Always_Succeeds_For_File_Or_Container()
    {
        foreach (var resourceType in new[] { WopiResourceType.File, WopiResourceType.Container })
        {
            var ctx = BuildContext(
                new WopiAuthorizeAttribute(resourceType, Permission.None),
                "id",
                Authenticated(new Claim(WopiClaimTypes.ResourceId, "id")));

            await _handler.HandleAsync(ctx);

            Assert.True(ctx.HasSucceeded);
        }
    }

    [Fact]
    public async Task Container_Update_Has_No_Mapping_So_Fails()
    {
        // Permission.Update doesn't map onto any WopiContainerPermissions flag (containers
        // don't expose a generic "update" surface — only Create/Delete/Rename), so the
        // switch falls through to the `_ => false` default arm.
        var ctx = BuildContext(
            new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Update),
            "c",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "c"),
                new Claim(WopiClaimTypes.ContainerPermissions,
                    (WopiContainerPermissions.UserCanCreateChildContainer |
                     WopiContainerPermissions.UserCanCreateChildFile |
                     WopiContainerPermissions.UserCanDelete |
                     WopiContainerPermissions.UserCanRename).ToString())));

        await _handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task File_Permissions_Claim_With_Garbage_Value_Falls_Back_To_Default_Flags()
    {
        // ReadFlags<T> returns default when Enum.TryParse fails, so a garbage claim is
        // equivalent to having no permissions — Update is rejected.
        var ctx = BuildContext(
            new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Update),
            "fileId",
            Authenticated(
                new Claim(WopiClaimTypes.ResourceId, "fileId"),
                new Claim(WopiClaimTypes.FilePermissions, "not-a-real-flag-value")));

        await _handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }
}
