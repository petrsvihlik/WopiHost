using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Controllers;
using WopiHost.Core.Models;

namespace WopiHost.Core.Tests.Controllers;

public class EcosystemControllerTests
{
    private readonly Mock<IWopiStorageProvider> _storage = new();
    private readonly Mock<IWopiFolder> _root = new();

    public EcosystemControllerTests()
    {
        _root.SetupGet(f => f.Identifier).Returns("root-id");
        _root.SetupGet(f => f.Name).Returns("Root");

        _storage.SetupGet(s => s.RootContainerPointer).Returns(_root.Object);
        _storage
            .Setup(s => s.GetWopiResource<IWopiFolder>("root-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_root.Object);
    }

    private EcosystemController BuildController()
    {
        // GetWopiSrc resolves the access token via AuthenticationService, so the
        // HttpContext needs a ServiceScopeFactory wired up with one.
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()))
            .ReturnsAsync(AuthenticateResult.NoResult());
        var ctx = new DefaultHttpContext
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope(auth.Object),
        };

        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("/wopi/containers/root-id");
        url.Setup(u => u.ActionContext).Returns(new ActionContext { HttpContext = ctx });

        return new EcosystemController(_storage.Object)
        {
            Url = url.Object,
            ControllerContext = new ControllerContext { HttpContext = ctx },
        };
    }

    [Fact]
    public async Task GetRootContainer_ReturnsRootContainerInfo()
    {
        var result = await BuildController().GetRootContainer();

        var json = Assert.IsType<JsonResult>(result);
        var info = Assert.IsType<RootContainerInfo>(json.Value);
        Assert.Equal("Root", info.ContainerPointer.Name);
    }

    [Fact]
    public async Task GetRootContainer_RootMissing_Throws()
    {
        _storage
            .Setup(s => s.GetWopiResource<IWopiFolder>("root-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IWopiFolder?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BuildController().GetRootContainer());
    }

    [Fact]
    public void CheckEcosystem_ReturnsCapabilitiesPayload()
    {
        var result = BuildController().CheckEcosystem();

        var json = Assert.IsType<JsonResult>(result);
        Assert.NotNull(json.Value);
    }
}
