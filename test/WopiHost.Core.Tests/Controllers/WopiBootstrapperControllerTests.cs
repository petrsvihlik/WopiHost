using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
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
    private readonly Mock<IUrlHelper> _url = new();

    public WopiBootstrapperControllerTests()
    {
        _rootFolder.SetupGet(f => f.Identifier).Returns("root-id");
        _rootFolder.SetupGet(f => f.Name).Returns("Root");
        _storage.SetupGet(s => s.RootContainerPointer).Returns(_rootFolder.Object);
        _storage
            .Setup(s => s.GetWopiResource<IWopiFolder>("root-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_rootFolder.Object);

        _permissions
            .Setup(p => p.GetFilePermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiFilePermissions.UserCanWrite);
        _permissions
            .Setup(p => p.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFolder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.UserCanCreateChildFile);

        // Default token issuance returns a deterministic value so tests can spot the
        // difference between "fresh" tokens and any inbound state.
        _tokens
            .Setup(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiAccessToken("FRESH-TOKEN", DateTimeOffset.UtcNow.AddMinutes(10)));
    }

    private WopiBootstrapperController BuildController(ClaimsPrincipal? user = null)
    {
        _url.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("https://wopi.example.com/wopi/anything");
        var serviceScope = TestUtils.CreateServiceScope(_permissions.Object, new Mock<IAuthenticationService>().Object);
        _url.Setup(u => u.ActionContext).Returns(new ActionContext
        {
            HttpContext = new DefaultHttpContext { ServiceScopeFactory = serviceScope }
        });

        return new WopiBootstrapperController(_storage.Object, _tokens.Object, _permissions.Object)
        {
            Url = _url.Object,
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

    // ---- Bootstrap (GET /wopibootstrapper) ---------------------------------------------------

    [Fact]
    public async Task Bootstrap_GET_ReturnsBareBootstrapInfo()
    {
        var result = await BuildController().Bootstrap();

        var json = Assert.IsType<JsonResult>(result);
        var info = Assert.IsType<BootstrapRootContainerInfo>(json.Value);
        Assert.NotNull(info.Bootstrap);
        Assert.Null(info.RootContainerInfo);
        Assert.Null(info.AccessTokenInfo);
        Assert.Equal("alice", info.Bootstrap!.UserId);
        Assert.Equal("alice@example.com", info.Bootstrap.SignInName);
        Assert.Equal("Alice Example", info.Bootstrap.UserFriendlyName);
    }

    [Fact]
    public async Task Bootstrap_GET_SerializesWithoutOptionalProperties()
    {
        // Spec sample: GET response is { "Bootstrap": {...} } only — no RootContainerInfo,
        // no AccessTokenInfo. Verify the wire format honors that via JsonIgnore(WhenWritingNull).
        var result = await BuildController().Bootstrap();
        var json = Assert.IsType<JsonResult>(result);

        var serialized = JsonSerializer.Serialize(json.Value);

        Assert.Contains("\"Bootstrap\"", serialized);
        Assert.DoesNotContain("RootContainerInfo", serialized);
        Assert.DoesNotContain("AccessTokenInfo", serialized);
    }

    // ---- EcosystemUrl token-trading prevention ----------------------------------------------

    [Fact]
    public async Task BootstrapInfo_EcosystemUrl_CarriesFreshMinimumPrivilegeToken()
    {
        // Per WOPI "preventing token trading" guidance, the access token embedded in
        // EcosystemUrl must be fresh and minimum-privilege. CheckEcosystem has no resource
        // gate, so the token is bound to the root container with WopiContainerPermissions.None.
        var capturedRequests = new List<WopiAccessTokenRequest>();
        _tokens
            .Setup(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .Callback<WopiAccessTokenRequest, CancellationToken>((req, _) => capturedRequests.Add(req))
            .ReturnsAsync(new WopiAccessToken("ECOSYSTEM-TOKEN", DateTimeOffset.UtcNow.AddMinutes(10)));

        await BuildController().Bootstrap();

        var ecosystemRequest = Assert.Single(capturedRequests);
        Assert.Equal("alice", ecosystemRequest.UserId);
        Assert.Equal("root-id", ecosystemRequest.ResourceId);
        Assert.Equal(WopiResourceType.Container, ecosystemRequest.ResourceType);
        Assert.Equal(WopiContainerPermissions.None, ecosystemRequest.ContainerPermissions);
    }

    // ---- GET_ROOT_CONTAINER (POST) ----------------------------------------------------------

    [Fact]
    public async Task ExecuteEcosystemOperation_GET_ROOT_CONTAINER_ReturnsBootstrapAndRootContainerInfo()
    {
        var result = await BuildController().ExecuteEcosystemOperation(ecosystemOperation: "GET_ROOT_CONTAINER");

        var json = Assert.IsType<JsonResult>(result);
        var info = Assert.IsType<BootstrapRootContainerInfo>(json.Value);
        Assert.NotNull(info.Bootstrap);
        Assert.NotNull(info.RootContainerInfo);
        Assert.Null(info.AccessTokenInfo);
        Assert.Equal("Root", info.RootContainerInfo!.ContainerPointer.Name);
        Assert.NotNull(info.RootContainerInfo.ContainerInfo);
        Assert.Equal("Root", info.RootContainerInfo.ContainerInfo!.Name);
    }

    [Fact]
    public async Task ExecuteEcosystemOperation_GET_ROOT_CONTAINER_ReturnsNotFound_WhenRootMissing()
    {
        _storage
            .Setup(s => s.GetWopiResource<IWopiFolder>("root-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IWopiFolder?)null);

        var result = await BuildController().ExecuteEcosystemOperation(ecosystemOperation: "GET_ROOT_CONTAINER");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ExecuteEcosystemOperation_GET_ROOT_CONTAINER_BindsContainerToken_WithRealPermissions()
    {
        var capturedRequests = new List<WopiAccessTokenRequest>();
        _tokens
            .Setup(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .Callback<WopiAccessTokenRequest, CancellationToken>((req, _) => capturedRequests.Add(req))
            .ReturnsAsync(new WopiAccessToken("T", DateTimeOffset.UtcNow));

        await BuildController().ExecuteEcosystemOperation(ecosystemOperation: "GET_ROOT_CONTAINER");

        // The bootstrap shell issues a None-perms token; the root container response then
        // issues a *separate* token with the user's real container permissions.
        Assert.Equal(2, capturedRequests.Count);
        var rootContainerToken = capturedRequests.Last();
        Assert.Equal("root-id", rootContainerToken.ResourceId);
        Assert.Equal(WopiResourceType.Container, rootContainerToken.ResourceType);
        Assert.Equal(WopiContainerPermissions.UserCanCreateChildFile, rootContainerToken.ContainerPermissions);
    }

    // ---- GET_NEW_ACCESS_TOKEN (POST) --------------------------------------------------------

    [Fact]
    public async Task ExecuteEcosystemOperation_GET_NEW_ACCESS_TOKEN_File_ReturnsAccessTokenInfo()
    {
        var file = new Mock<IWopiFile>();
        file.SetupGet(f => f.Identifier).Returns("file-99");
        _storage.Setup(s => s.GetWopiResource<IWopiFile>("file-99", It.IsAny<CancellationToken>())).ReturnsAsync(file.Object);

        var expires = DateTimeOffset.UtcNow.AddMinutes(10);
        var capturedRequests = new List<WopiAccessTokenRequest>();
        _tokens
            .Setup(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .Callback<WopiAccessTokenRequest, CancellationToken>((req, _) => capturedRequests.Add(req))
            .ReturnsAsync((WopiAccessTokenRequest req, CancellationToken _) =>
                req.ResourceType == WopiResourceType.File
                    ? new WopiAccessToken("FILE-TOKEN", expires)
                    : new WopiAccessToken("ECOSYSTEM-TOKEN", expires));

        var result = await BuildController().ExecuteEcosystemOperation(
            ecosystemOperation: "GET_NEW_ACCESS_TOKEN",
            wopiSrc: "https://wopi.example.com/wopi/files/file-99");

        var info = Assert.IsType<BootstrapRootContainerInfo>(((JsonResult)result).Value);
        Assert.Equal("FILE-TOKEN", info.AccessTokenInfo?.AccessToken);
        Assert.Equal(expires.ToUnixTimeSeconds(), info.AccessTokenInfo?.AccessTokenExpiry);
        Assert.Null(info.RootContainerInfo);

        // Verify a file token was issued with real permissions baked in.
        var fileTokenReq = Assert.Single(capturedRequests, r => r.ResourceType == WopiResourceType.File);
        Assert.Equal("file-99", fileTokenReq.ResourceId);
        Assert.Equal(WopiFilePermissions.UserCanWrite, fileTokenReq.FilePermissions);
    }

    [Fact]
    public async Task ExecuteEcosystemOperation_GET_NEW_ACCESS_TOKEN_Container_ReturnsAccessTokenInfo()
    {
        var folder = new Mock<IWopiFolder>();
        folder.SetupGet(f => f.Identifier).Returns("container-7");
        _storage.Setup(s => s.GetWopiResource<IWopiFolder>("container-7", It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);

        var capturedRequests = new List<WopiAccessTokenRequest>();
        _tokens
            .Setup(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .Callback<WopiAccessTokenRequest, CancellationToken>((req, _) => capturedRequests.Add(req))
            .ReturnsAsync(new WopiAccessToken("T", DateTimeOffset.UtcNow));

        var result = await BuildController().ExecuteEcosystemOperation(
            ecosystemOperation: "GET_NEW_ACCESS_TOKEN",
            wopiSrc: "https://wopi.example.com/wopi/containers/container-7");

        var info = Assert.IsType<BootstrapRootContainerInfo>(((JsonResult)result).Value);
        Assert.NotNull(info.AccessTokenInfo);

        // Container-bound token with real container permissions.
        var containerReq = Assert.Single(capturedRequests, r => r.ResourceType == WopiResourceType.Container && r.ResourceId == "container-7");
        Assert.Equal(WopiContainerPermissions.UserCanCreateChildFile, containerReq.ContainerPermissions);
    }

    [Fact]
    public async Task ExecuteEcosystemOperation_GET_NEW_ACCESS_TOKEN_StripsQueryAndDecodesIdFromWopiSrc()
    {
        _storage.Setup(s => s.GetWopiResource<IWopiFile>("some file", It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IWopiFile>());

        var result = await BuildController().ExecuteEcosystemOperation(
            ecosystemOperation: "GET_NEW_ACCESS_TOKEN",
            wopiSrc: "https://wopi.example.com/wopi/files/some%20file?access_token=ignored");

        Assert.IsType<JsonResult>(result);
        _storage.Verify(s => s.GetWopiResource<IWopiFile>("some file", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteEcosystemOperation_GET_NEW_ACCESS_TOKEN_ReturnsNotFound_WhenWopiSrcMissing()
    {
        // Spec: "if the X-WOPI-WopiSrc header is not present, the host should return a 404 Not Found"
        var result = await BuildController().ExecuteEcosystemOperation(ecosystemOperation: "GET_NEW_ACCESS_TOKEN", wopiSrc: null);

        Assert.IsType<NotFoundResult>(result);
        _tokens.Verify(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteEcosystemOperation_GET_NEW_ACCESS_TOKEN_ReturnsNotFound_WhenWopiSrcIsMalformed()
    {
        // Not a /wopi/files/{id} or /wopi/containers/{id} URL.
        var result = await BuildController().ExecuteEcosystemOperation(
            ecosystemOperation: "GET_NEW_ACCESS_TOKEN",
            wopiSrc: "https://wopi.example.com/elsewhere/file");

        Assert.IsType<NotFoundResult>(result);
        _tokens.Verify(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteEcosystemOperation_GET_NEW_ACCESS_TOKEN_ReturnsNotFound_WhenFileMissing()
    {
        // Spec: "must only provide a WOPI access token if the requested WopiSrc exists"
        _storage
            .Setup(s => s.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IWopiFile?)null);

        var result = await BuildController().ExecuteEcosystemOperation(
            ecosystemOperation: "GET_NEW_ACCESS_TOKEN",
            wopiSrc: "https://wopi.example.com/wopi/files/missing");

        Assert.IsType<NotFoundResult>(result);
        // The bootstrap-shell ecosystem token may have been issued before we discover
        // the missing resource — assert no FILE token was issued.
        _tokens.Verify(t => t.IssueAsync(
            It.Is<WopiAccessTokenRequest>(r => r.ResourceType == WopiResourceType.File),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteEcosystemOperation_GET_NEW_ACCESS_TOKEN_ReturnsNotFound_WhenContainerMissing()
    {
        _storage
            .Setup(s => s.GetWopiResource<IWopiFolder>("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IWopiFolder?)null);

        var result = await BuildController().ExecuteEcosystemOperation(
            ecosystemOperation: "GET_NEW_ACCESS_TOKEN",
            wopiSrc: "https://wopi.example.com/wopi/containers/missing");

        Assert.IsType<NotFoundResult>(result);
    }

    // ---- Unknown / missing operation --------------------------------------------------------

    [Fact]
    public async Task ExecuteEcosystemOperation_UnknownOperation_ReturnsNotImplemented()
    {
        var result = await BuildController().ExecuteEcosystemOperation(ecosystemOperation: "SOMETHING_ELSE");

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task ExecuteEcosystemOperation_NoOperationHeader_ReturnsNotImplemented()
    {
        var result = await BuildController().ExecuteEcosystemOperation(ecosystemOperation: null);

        Assert.IsType<NotImplementedResult>(result);
    }

    // ---- Principal claim handling -----------------------------------------------------------

    [Fact]
    public async Task PrincipalWithoutNameIdentifier_FallsBackToUpnClaim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Upn, "upn-user"),
        ], "bootstrap"));

        var result = await BuildController(user).ExecuteEcosystemOperation(ecosystemOperation: "GET_ROOT_CONTAINER");

        var info = Assert.IsType<BootstrapRootContainerInfo>(((JsonResult)result).Value);
        Assert.Equal("upn-user", info.Bootstrap?.UserId);
    }

    [Fact]
    public async Task PrincipalWithNoIdentifier_Throws()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity("bootstrap"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BuildController(user).ExecuteEcosystemOperation(ecosystemOperation: "GET_ROOT_CONTAINER"));
    }

    // ---- TryParseWopiSrc edge cases ---------------------------------------------------------

    [Theory]
    [InlineData("https://wopi.example.com/wopi/files/abc", WopiResourceType.File, "abc")]
    [InlineData("https://wopi.example.com/wopi/containers/abc", WopiResourceType.Container, "abc")]
    [InlineData("https://wopi.example.com/wopi/files/abc?access_token=t", WopiResourceType.File, "abc")]
    [InlineData("https://wopi.example.com/wopi/files/some%20file", WopiResourceType.File, "some file")]
    [InlineData("https://wopi.example.com/some/wopi/Files/CASE_INSENSITIVE", WopiResourceType.File, "CASE_INSENSITIVE")]
    public void TryParseWopiSrc_ValidUrls(string url, WopiResourceType expectedType, string expectedId)
    {
        var ok = WopiBootstrapperController.TryParseWopiSrc(url, out var type, out var id);

        Assert.True(ok);
        Assert.Equal(expectedType, type);
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("https://wopi.example.com/elsewhere/abc")]
    [InlineData("https://wopi.example.com/wopi/files/")] // missing id
    [InlineData("https://wopi.example.com/wopi/files")] // missing id segment entirely
    public void TryParseWopiSrc_RejectsInvalidUrls(string url)
    {
        var ok = WopiBootstrapperController.TryParseWopiSrc(url, out _, out _);

        Assert.False(ok);
    }
}
