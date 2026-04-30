using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Controllers;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Controllers;

public class EcosystemControllerTests
{
    private readonly Mock<IWopiStorageProvider> _storage = new();
    private readonly Mock<IWopiAccessTokenService> _tokens = new();
    private readonly Mock<IWopiPermissionProvider> _permissions = new();
    private readonly Mock<IWopiFolder> _root = new();

    public EcosystemControllerTests()
    {
        _root.SetupGet(f => f.Identifier).Returns("root-id");
        _root.SetupGet(f => f.Name).Returns("Root");
        _storage.SetupGet(s => s.RootContainerPointer).Returns(_root.Object);
        _storage
            .Setup(s => s.GetWopiResource<IWopiFolder>("root-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_root.Object);

        _permissions
            .Setup(p => p.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFolder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.UserCanCreateChildFile);

        _tokens
            .Setup(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiAccessToken("FRESH-TOKEN", DateTimeOffset.UtcNow.AddMinutes(10)));
    }

    private EcosystemController BuildController(
        ClaimsPrincipal? user = null,
        WopiHostOptions? options = null,
        UrlRouteContext? captureUrlRouteContext = null)
    {
        var optionsObj = Options.Create(options ?? DefaultOptions());

        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()))
            .ReturnsAsync(AuthenticateResult.NoResult());

        var scope = TestUtils.CreateServiceScope(_permissions.Object, optionsObj, auth.Object);
        var ctx = new DefaultHttpContext
        {
            ServiceScopeFactory = scope,
            User = user ?? AuthenticatedUser(),
        };

        var url = new Mock<IUrlHelper>();
        var setup = url.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>()));
        if (captureUrlRouteContext is not null)
        {
            setup.Callback<UrlRouteContext>(rc =>
            {
                captureUrlRouteContext.RouteName = rc.RouteName;
                captureUrlRouteContext.Values = rc.Values;
                captureUrlRouteContext.Host = rc.Host;
                captureUrlRouteContext.Protocol = rc.Protocol;
                captureUrlRouteContext.Fragment = rc.Fragment;
            }).Returns("/wopi/containers/root-id");
        }
        else
        {
            setup.Returns("/wopi/containers/root-id");
        }
        url.Setup(u => u.ActionContext).Returns(new ActionContext { HttpContext = ctx });

        return new EcosystemController(_storage.Object, _tokens.Object, _permissions.Object, optionsObj)
        {
            Url = url.Object,
            ControllerContext = new ControllerContext { HttpContext = ctx },
        };
    }

    private static WopiHostOptions DefaultOptions() => new()
    {
        StorageProviderAssemblyName = "Test",
        ClientUrl = new Uri("http://localhost"),
    };

    private static ClaimsPrincipal AuthenticatedUser() => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, "alice"),
        new Claim(ClaimTypes.Name, "Alice Example"),
        new Claim(ClaimTypes.Email, "alice@example.com"),
    ], "Test"));

    [Fact]
    public async Task GetRootContainer_ReturnsRootContainerInfo()
    {
        var result = await BuildController().GetRootContainer();

        var json = Assert.IsType<JsonResult>(result);
        var info = Assert.IsType<RootContainerInfo>(json.Value);
        Assert.Equal("Root", info.ContainerPointer.Name);
    }

    [Fact]
    public async Task GetRootContainer_PopulatesContainerInfo()
    {
        var result = await BuildController().GetRootContainer();

        var info = Assert.IsType<RootContainerInfo>(((JsonResult)result).Value);
        Assert.NotNull(info.ContainerInfo);
        Assert.Equal("Root", info.ContainerInfo!.Name);
    }

    [Fact]
    public async Task GetRootContainer_ContainerInfo_PropagatesUserCanFlagsFromPermissionProvider()
    {
        _permissions
            .Setup(p => p.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFolder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.UserCanCreateChildFile | WopiContainerPermissions.UserCanRename);

        var result = await BuildController().GetRootContainer();

        var info = Assert.IsType<RootContainerInfo>(((JsonResult)result).Value);
        Assert.True(info.ContainerInfo!.UserCanCreateChildFile);
        Assert.True(info.ContainerInfo!.UserCanRename);
        Assert.False(info.ContainerInfo!.UserCanCreateChildContainer);
        Assert.False(info.ContainerInfo!.UserCanDelete);
    }

    [Fact]
    public async Task GetRootContainer_IssuesPerContainerToken_BoundToCallerAndRoot()
    {
        await BuildController().GetRootContainer();

        _tokens.Verify(t => t.IssueAsync(
            It.Is<WopiAccessTokenRequest>(r =>
                r.UserId == "alice" &&
                r.UserDisplayName == "Alice Example" &&
                r.UserEmail == "alice@example.com" &&
                r.ResourceId == "root-id" &&
                r.ResourceType == WopiResourceType.Container &&
                r.ContainerPermissions == WopiContainerPermissions.UserCanCreateChildFile),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRootContainer_ContainerPointerUrl_CarriesFreshAccessToken_NotInbound()
    {
        // Token-trading prevention per https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts#preventing-token-trading
        // The handed-off URL must contain a token issued for this container, not the
        // caller's inbound token.
        var captured = new UrlRouteContext();

        await BuildController(captureUrlRouteContext: captured).GetRootContainer();

        Assert.Equal("CheckContainerInfo", captured.RouteName);
        var values = new RouteValueDictionary(captured.Values);
        Assert.Equal("FRESH-TOKEN", values["access_token"]);
        Assert.Equal("root-id", values["id"]);
    }

    [Fact]
    public async Task GetRootContainer_ReturnsNotFound_WhenStorageReturnsNull()
    {
        _storage
            .Setup(s => s.GetWopiResource<IWopiFolder>("root-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IWopiFolder?)null);

        var result = await BuildController().GetRootContainer();

        Assert.IsType<NotFoundResult>(result);
        // No token should be issued when there is no resource to bind it to.
        _tokens.Verify(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckEcosystem_ReturnsTypedDtoWithSupportsContainersTrue()
    {
        var result = await BuildController().CheckEcosystem();

        var json = Assert.IsType<JsonResult>(result);
        var dto = Assert.IsType<WopiCheckEcosystem>(json.Value);
        Assert.True(dto.SupportsContainers);
    }

    [Fact]
    public async Task CheckEcosystem_AgreesWithCheckFileInfoOnSupportsContainers_ByDefault()
    {
        // Spec: SupportsContainers in CheckEcosystem "should match" the CheckFileInfo value.
        // Both default to WopiHostCapabilities.SupportsContainers (true) so they agree.
        var defaults = new WopiHostCapabilities();

        var dto = Assert.IsType<WopiCheckEcosystem>(((JsonResult)await BuildController().CheckEcosystem()).Value);
        Assert.Equal(defaults.SupportsContainers, dto.SupportsContainers);
    }

    [Fact]
    public async Task CheckEcosystem_RunsThroughOnCheckEcosystemHook()
    {
        var hookCalled = false;
        var options = DefaultOptions();
        options.OnCheckEcosystem = ctx =>
        {
            hookCalled = true;
            ctx.CheckEcosystem.SupportsContainers = false;
            return Task.FromResult(ctx.CheckEcosystem);
        };

        var result = await BuildController(options: options).CheckEcosystem();

        var dto = Assert.IsType<WopiCheckEcosystem>(((JsonResult)result).Value);
        Assert.True(hookCalled);
        Assert.False(dto.SupportsContainers);
    }

    [Fact]
    public async Task CheckEcosystem_HookReceivesCurrentUser()
    {
        ClaimsPrincipal? observed = null;
        var options = DefaultOptions();
        options.OnCheckEcosystem = ctx =>
        {
            observed = ctx.User;
            return Task.FromResult(ctx.CheckEcosystem);
        };

        await BuildController(options: options).CheckEcosystem();

        Assert.NotNull(observed);
        Assert.Equal("alice", observed!.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
#pragma warning disable WOPIHOST001 // GetFileWopiSrc is reserved for future use; suppression is the documented opt-in.
    public void GetFileWopiSrc_ReturnsNotImplemented_BySpecReservation()
    {
        // The Microsoft spec explicitly tells WOPI clients not to call this operation today.
        // Until that changes, the host returns 501 (which the spec allows) and the
        // SupportsGetFileWopiSrc capability flag stays false so the operation is not advertised.
        var result = BuildController().GetFileWopiSrc(hostNativeFileName: "anything");

        Assert.IsType<NotImplementedResult>(result);
    }
#pragma warning restore WOPIHOST001
}
