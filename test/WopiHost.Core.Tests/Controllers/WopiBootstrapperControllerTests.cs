using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Controllers;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Controllers;

public class WopiBootstrapperControllerTests
{
    private readonly Mock<IWopiStorageProvider> _storage = new();
    private readonly Mock<IWopiAccessTokenService> _tokens = new();
    private readonly Mock<IWopiPermissionProvider> _permissions = new();
    private readonly Mock<IWopiFolder> _rootFolder = new();

    public WopiBootstrapperControllerTests()
    {
        _rootFolder.SetupGet(f => f.Identifier).Returns("root-id");
        _rootFolder.SetupGet(f => f.Name).Returns("Root");
        _storage.SetupGet(s => s.RootContainerPointer).Returns(_rootFolder.Object);

        _permissions
            .Setup(p => p.GetFilePermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiFilePermissions.UserCanWrite);
        _permissions
            .Setup(p => p.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFolder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.UserCanCreateChildFile);
    }

    private WopiBootstrapperController BuildController(ClaimsPrincipal? user = null)
    {
        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("https://wopi.example.com/wopi/anything");
        var serviceScope = TestUtils.CreateServiceScope(new Mock<IAuthenticationService>().Object);
        url.Setup(u => u.ActionContext).Returns(new ActionContext
        {
            HttpContext = new DefaultHttpContext { ServiceScopeFactory = serviceScope }
        });

        return new WopiBootstrapperController(_storage.Object, _tokens.Object, _permissions.Object)
        {
            Url = url.Object,
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    ServiceScopeFactory = serviceScope,
                    User = user ?? AuthenticatedUser(),
                },
            }
        };
    }

    private static ClaimsPrincipal AuthenticatedUser() => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, "alice"),
        new Claim(ClaimTypes.Name, "Alice Example"),
        new Claim(ClaimTypes.Email, "alice@example.com"),
    ], "bootstrap"));

    [Fact]
    public async Task GetRootContainer_ReturnsRootContainerInfo()
    {
        _tokens
            .Setup(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiAccessToken("ROOT-TOKEN", DateTimeOffset.UtcNow.AddMinutes(10)));

        var result = await BuildController().GetRootContainer(ecosystemOperation: "GET_ROOT_CONTAINER");

        var json = Assert.IsType<JsonResult>(result);
        var info = Assert.IsType<BootstrapRootContainerInfo>(json.Value);
        Assert.NotNull(info.RootContainerInfo);
        Assert.Equal("Root", info.RootContainerInfo!.ContainerPointer.Name);
        Assert.Equal("alice", info.Bootstrap?.UserId);
        Assert.Equal("Alice Example", info.Bootstrap?.UserFriendlyName);
        Assert.Equal("alice@example.com", info.Bootstrap?.SignInName);

        _tokens.Verify(t => t.IssueAsync(
            It.Is<WopiAccessTokenRequest>(r =>
                r.UserId == "alice" &&
                r.ResourceId == "root-id" &&
                r.ResourceType == WopiResourceType.Container &&
                r.ContainerPermissions == WopiContainerPermissions.UserCanCreateChildFile),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetNewAccessToken_ReturnsAccessTokenInfo_WithFilePermissionsBaked()
    {
        var file = new Mock<IWopiFile>();
        file.SetupGet(f => f.Identifier).Returns("file-99");
        _storage.Setup(s => s.GetWopiResource<IWopiFile>("file-99", It.IsAny<CancellationToken>())).ReturnsAsync(file.Object);

        var expires = DateTimeOffset.UtcNow.AddMinutes(10);
        _tokens
            .Setup(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiAccessToken("FILE-TOKEN", expires));

        var result = await BuildController().GetRootContainer(
            ecosystemOperation: "GET_NEW_ACCESS_TOKEN",
            wopiSrc: "https://wopi.example.com/wopi/files/file-99");

        var json = Assert.IsType<JsonResult>(result);
        var info = Assert.IsType<BootstrapRootContainerInfo>(json.Value);
        Assert.Equal("FILE-TOKEN", info.AccessTokenInfo?.AccessToken);
        Assert.Equal(expires.ToUnixTimeSeconds(), info.AccessTokenInfo?.AccessTokenExpiry);

        _tokens.Verify(t => t.IssueAsync(
            It.Is<WopiAccessTokenRequest>(r =>
                r.ResourceId == "file-99" &&
                r.ResourceType == WopiResourceType.File &&
                r.FilePermissions == WopiFilePermissions.UserCanWrite),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetNewAccessToken_StripsQueryAndDecodesIdFromWopiSrc()
    {
        _storage.Setup(s => s.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IWopiFile>());
        _tokens
            .Setup(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiAccessToken("T", DateTimeOffset.UtcNow));

        await BuildController().GetRootContainer(
            ecosystemOperation: "GET_NEW_ACCESS_TOKEN",
            wopiSrc: "https://wopi.example.com/wopi/files/some%20file?access_token=ignored");

        _tokens.Verify(t => t.IssueAsync(
            It.Is<WopiAccessTokenRequest>(r => r.ResourceId == "some file"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetNewAccessToken_RequiresWopiSrcHeader()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            BuildController().GetRootContainer(ecosystemOperation: "GET_NEW_ACCESS_TOKEN", wopiSrc: null));
    }

    [Fact]
    public async Task UnknownEcosystemOperation_ReturnsNotImplemented()
    {
        var result = await BuildController().GetRootContainer(ecosystemOperation: "SOMETHING_ELSE");

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task PrincipalWithoutNameIdentifier_FallsBackToUpnClaim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Upn, "upn-user"),
        ], "bootstrap"));
        _tokens
            .Setup(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiAccessToken("T", DateTimeOffset.UtcNow));

        var result = await BuildController(user).GetRootContainer(ecosystemOperation: "GET_ROOT_CONTAINER");

        var json = Assert.IsType<JsonResult>(result);
        var info = Assert.IsType<BootstrapRootContainerInfo>(json.Value);
        Assert.Equal("upn-user", info.Bootstrap?.UserId);
    }

    [Fact]
    public async Task PrincipalWithNoIdentifier_Throws()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity("bootstrap"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BuildController(user).GetRootContainer(ecosystemOperation: "GET_ROOT_CONTAINER"));
    }

    [Fact]
    public async Task GetNewAccessToken_With_Unknown_File_Issues_Token_Without_Permissions()
    {
        // Storage returns null when the wopiSrc resource doesn't exist; the helper still
        // issues a token (with WopiFilePermissions.None) so the WOPI client can call back
        // through the regular endpoints and get a proper 404.
        _storage
            .Setup(s => s.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IWopiFile?)null);
        _tokens
            .Setup(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiAccessToken("T", DateTimeOffset.UtcNow));

        var result = await BuildController().GetRootContainer(
            ecosystemOperation: "GET_NEW_ACCESS_TOKEN",
            wopiSrc: "https://wopi.example.com/wopi/files/missing");

        Assert.IsType<JsonResult>(result);
        _tokens.Verify(t => t.IssueAsync(
            It.Is<WopiAccessTokenRequest>(r =>
                r.ResourceId == "missing" &&
                r.FilePermissions == WopiFilePermissions.None),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
