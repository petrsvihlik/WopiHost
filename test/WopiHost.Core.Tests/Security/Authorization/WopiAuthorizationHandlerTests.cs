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
    public async Task Fails_When_Token_Bound_To_Different_Resource()
    {
        var requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Read);
        var ctx = BuildContext(requirement, "fileId",
            Authenticated(new Claim(WopiClaimTypes.ResourceId, "other-file")));

        await _handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
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
}
