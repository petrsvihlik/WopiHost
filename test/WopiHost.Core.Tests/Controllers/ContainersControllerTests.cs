using System.Collections.ObjectModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Controllers;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Controllers;

public class ContainersControllerTests
{
    private readonly Mock<IWopiStorageProvider> storageProviderMock;
    private readonly Mock<IWopiLockProvider> lockProviderMock;
    private readonly Mock<IWopiWritableStorageProvider> writableStorageProviderMock;
    private readonly Mock<IWopiPermissionProvider> permissionProviderMock;
    private readonly Mock<IWopiAccessTokenService> accessTokenServiceMock;
    private readonly Mock<IUrlHelper> urlMock;
    private readonly ContainersController _controller;

    public ContainersControllerTests()
    {
        storageProviderMock = new Mock<IWopiStorageProvider>();
        lockProviderMock = new Mock<IWopiLockProvider>();
        writableStorageProviderMock = new Mock<IWopiWritableStorageProvider>();
        permissionProviderMock = new Mock<IWopiPermissionProvider>();
        permissionProviderMock
            .Setup(_ => _.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFolder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.UserCanCreateChildContainer | WopiContainerPermissions.UserCanCreateChildFile | WopiContainerPermissions.UserCanDelete | WopiContainerPermissions.UserCanRename);
        accessTokenServiceMock = new Mock<IWopiAccessTokenService>();
        accessTokenServiceMock
            .Setup(s => s.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiAccessToken("FRESH-TOKEN", DateTimeOffset.UtcNow.AddMinutes(10)));
        urlMock = new Mock<IUrlHelper>();
        urlMock
            .Setup(_ => _.RouteUrl(It.IsAny<UrlRouteContext>()))
            .Returns("https://localhost");
        urlMock
            .Setup(_ => _.ActionContext)
            .Returns(new ActionContext
            {
                HttpContext = new DefaultHttpContext()
                {
                    ServiceScopeFactory = TestUtils.CreateServiceScope(new Mock<IAuthenticationService>().Object),
                }
            });
        _controller = new ContainersController(
            storageProviderMock.Object,
            lockProviderMock.Object,
            writableStorageProviderMock.Object)
        {
            Url = urlMock.Object,
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
                {
                    ServiceScopeFactory = TestUtils.CreateServiceScope(permissionProviderMock.Object),
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "alice"),
                        new Claim(ClaimTypes.Name, "Alice Example"),
                        new Claim(ClaimTypes.Email, "alice@example.com"),
                    ], "Test")),
                }
            }
        };
    }

    [Fact]
    public async Task CheckContainerInfo_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.CheckContainerInfo("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CheckContainerInfo_ReturnsContainerInfo_WhenContainerExists()
    {
        var container = new Mock<IWopiFolder>();
        container.Setup(c => c.Name).Returns("TestContainer");
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => container.Object);

        var result = await _controller.CheckContainerInfo("existing");

        var jsonResult = Assert.IsType<JsonResult<WopiCheckContainerInfo>>(result);
        var checkContainerInfo = Assert.IsType<WopiCheckContainerInfo>(jsonResult.Value);
        Assert.Equal("TestContainer", checkContainerInfo.Name);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsNotImplemented_WhenWritableStorageProviderIsNull()
    {
        var controller = new ContainersController(
            storageProviderMock.Object,
            lockProviderMock.Object,
            null);

        var result = await controller.CreateChildContainer("id");

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.CreateChildContainer("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsNotImplemented_WhenBothHeadersArePresent()
    {
        var containerId = "existing-container";
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);

        var result = await _controller.CreateChildContainer(containerId, UtfString.FromDecoded("suggestedTarget"), UtfString.FromDecoded("relativeTarget"));

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsNotImplemented_WhenBothHeadersAreMissing()
    {
        var containerId = "existing-container";
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);

        var result = await _controller.CreateChildContainer(containerId);

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsBadRequest_WhenNameIsInvalid()
    {
        var containerId = "existing-container";
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.CreateChildContainer(containerId, UtfString.FromDecoded("suggestedTarget"));

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsConflict_WhenContainerAlreadyExists()
    {
        var containerId = "existing-container";
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        storageProviderMock.Setup(sp => sp.GetWopiResourceByName<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);

        var result = await _controller.CreateChildContainer(containerId, relativeTarget: UtfString.FromDecoded("relativeTarget"));

        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsInternalServerError_WhenNewFolderIsNull()
    {
        var containerId = "existing-container";
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        storageProviderMock.Setup(sp => sp.GetWopiResourceByName<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.CreateChildContainer(containerId, relativeTarget: UtfString.FromDecoded("relativeTarget"));

        Assert.IsType<InternalServerErrorResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsJsonResult_WhenSuccessful()
    {
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        container.Setup(c => c.Name).Returns("container");
        container.Setup(c => c.Identifier).Returns(containerId);
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        storageProviderMock.Setup(sp => sp.GetWopiResourceByName<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);
        writableStorageProviderMock.Setup(_ => _.CreateWopiChildResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(container.Object);

        var result = await _controller.CreateChildContainer(containerId, relativeTarget: UtfString.FromDecoded("relativeTarget"));

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<CreateChildContainerResponse>(jsonResult.Value);
    }

    [Fact]
    public async Task CreateChildContainer_SuggestedTarget_ReturnsJsonResult_WhenSuccessful()
    {
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        container.Setup(c => c.Name).Returns("container");
        container.Setup(c => c.Identifier).Returns(containerId);
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        writableStorageProviderMock.Setup(wsp => wsp.GetSuggestedName<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("suggestedTarget");
        writableStorageProviderMock.Setup(_ => _.CreateWopiChildResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(container.Object);

        var result = await _controller.CreateChildContainer(containerId, suggestedTarget: UtfString.FromDecoded("suggestedTarget"));

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<CreateChildContainerResponse>(jsonResult.Value);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsNotImplemented_WhenWritableStorageProviderIsNull()
    {
        var controller = new ContainersController(storageProviderMock.Object, lockProviderMock.Object, null);

        var result = await controller.CreateChildFile("containerId");

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);

        var result = await _controller.CreateChildFile("containerId");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsNotImplemented_WhenBothHeadersArePresent()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);

        var result = await _controller.CreateChildFile("containerId", UtfString.FromDecoded("suggestedTarget"), UtfString.FromDecoded("relativeTarget"));

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsNotImplemented_WhenBothHeadersAreMissing()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);

        var result = await _controller.CreateChildFile("containerId");

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsBadRequest_WhenNameIsInvalid()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.CreateChildFile("containerId", UtfString.FromDecoded("relativeTarget"));

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsConflict_WhenFileAlreadyExistsAndOverwriteIsFalse()
    {
        var existingFile = new Mock<IWopiFile>().Object;
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        storageProviderMock.Setup(sp => sp.GetWopiResourceByName<IWopiFile>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.CreateChildFile("containerId", relativeTarget: UtfString.FromDecoded("relativeTarget"), overwriteRelativeTarget: false);

        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsLockMismatch_WhenFileIsLocked()
    {
        var existingFile = new Mock<IWopiFile>().Object;
        var lockInfo = new WopiLockInfo { FileId = "any", LockId = "lockId" };
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        storageProviderMock.Setup(sp => sp.GetWopiResourceByName<IWopiFile>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        lockProviderMock.Setup(lp => lp.GetLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockInfo);

        var result = await _controller.CreateChildFile("containerId", relativeTarget: UtfString.FromDecoded("relativeTarget"), overwriteRelativeTarget: true);

        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsJsonResult_WhenFileIsCreated()
    {
        var fileMock = new Mock<IWopiFile>();
        fileMock.SetupGet(f => f.Owner).Returns("ownerId");
        fileMock.SetupGet(f => f.Version).Returns("1.0");
        fileMock.SetupGet(f => f.Name).Returns("test");
        fileMock.SetupGet(f => f.Extension).Returns("txt");
        fileMock.SetupGet(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.SetupGet(f => f.Length).Returns(1024);
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        writableStorageProviderMock.Setup(wsp => wsp.CreateWopiChildResource<IWopiFile>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        var result = await _controller.CreateChildFile("containerId", relativeTarget: UtfString.FromDecoded("relativeTarget"));

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<ChildFile>(jsonResult.Value);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsNotImplemented_WhenWritableStorageProviderIsNull()
    {
        var controller = new ContainersController(
            storageProviderMock.Object,
            lockProviderMock.Object,
            null);

        var result = await controller.DeleteContainer("id");

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.DeleteContainer("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsOk_WhenContainerExist()
    {
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        writableStorageProviderMock.Setup(_ => _.DeleteWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DeleteContainer(containerId);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsInternalServerError_WhenDeleteFails()
    {
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        writableStorageProviderMock.Setup(_ => _.DeleteWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.DeleteContainer(containerId);

        Assert.IsType<InternalServerErrorResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsNotFound_WhenDeleteFailsWithDirectoryNotFoundException()
    {
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        writableStorageProviderMock.Setup(_ => _.DeleteWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DirectoryNotFoundException());

        var result = await _controller.DeleteContainer(containerId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsConflict_WhenDeleteFailsWithInvalidOperationException()
    {
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        writableStorageProviderMock.Setup(_ => _.DeleteWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        var result = await _controller.DeleteContainer(containerId);

        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsNotImplemented_WhenWritableStorageProviderIsNull()
    {
        var controller = new ContainersController(
            storageProviderMock.Object,
            lockProviderMock.Object,
            null);

        var result = await controller.RenameContainer("id", new UtfString());

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsNotFound_WhenContainerNotFound()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.RenameContainer("id", new UtfString());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsBadRequest_WhenRequestedNameIsIllegal()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("illegalName"));

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal("Specified name is illegal", _controller.Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME]);
    }

    [Fact]
    public async Task RenameContainer_ReturnsConflict_WhenRequestedNameAlreadyExists()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("existingName"));

        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsNotFound_WhenDirectoryNotFoundException()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DirectoryNotFoundException());

        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("existingName"));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsInternalServerError_WhenUnexpectedErrorOccurs()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());

        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("newName"));

        Assert.IsType<InternalServerErrorResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsJsonResult_WhenRenameIsSuccessful()
    {
        var container = new Mock<IWopiFolder>();
        container.SetupGet(c => c.Name).Returns("newName");
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("newName"));

        var jsonResult = Assert.IsType<JsonResult>(result);
    }

    [Fact]
    public async Task GetEcosystem_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.GetEcosystem("nonexistent", accessTokenServiceMock.Object);

        Assert.IsType<NotFoundResult>(result);
        accessTokenServiceMock.Verify(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetEcosystem_ReturnsLink()
    {
        var containerId = "existing-container";
        var folderMock = new Mock<IWopiFolder>();
        folderMock.SetupGet(f => f.Identifier).Returns(containerId);
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(folderMock.Object);

        var result = await _controller.GetEcosystem(containerId, accessTokenServiceMock.Object);

        var jsonResult = Assert.IsType<JsonResult<UrlResponse>>(result);
        Assert.IsType<UrlResponse>(jsonResult.Value);
    }

    [Fact]
    public async Task GetEcosystem_IssuesFreshMinimumPrivilegeToken_NotInbound()
    {
        // Token-trading prevention: the URL handed back to the WOPI client must carry a
        // FRESH access token, not the inbound one, and that token must grant the minimum
        // privileges required by the URL it lives in (CheckEcosystem -> None).
        // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts#preventing-token-trading
        var containerId = "existing-container";
        var folderMock = new Mock<IWopiFolder>();
        folderMock.SetupGet(f => f.Identifier).Returns(containerId);
        storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folderMock.Object);

        UrlRouteContext? captured = null;
        urlMock
            .Setup(_ => _.RouteUrl(It.IsAny<UrlRouteContext>()))
            .Callback<UrlRouteContext>(rc => captured = rc)
            .Returns("https://localhost/wopi/ecosystem");

        await _controller.GetEcosystem(containerId, accessTokenServiceMock.Object);

        accessTokenServiceMock.Verify(t => t.IssueAsync(
            It.Is<WopiAccessTokenRequest>(r =>
                r.UserId == "alice" &&
                r.ResourceId == containerId &&
                r.ResourceType == WopiResourceType.Container &&
                r.ContainerPermissions == WopiContainerPermissions.None),
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(captured);
        var values = new RouteValueDictionary(captured!.Values);
        Assert.Equal("FRESH-TOKEN", values["access_token"]);
    }

    [Fact]
    public async Task EnumerateAncestors_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.EnumerateAncestors("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EnumerateAncestors_ReturnsAncestors_WhenContainerExists()
    {
        var containerId = "existing-container";
        var ancestors = new ReadOnlyCollection<IWopiFolder>(
        [
            new Mock<IWopiFolder>().Object,
            new Mock<IWopiFolder>().Object
        ]);
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        storageProviderMock.Setup(sp => sp.GetAncestors<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(ancestors);

        var result = await _controller.EnumerateAncestors(containerId);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var response = Assert.IsType<EnumerateAncestorsResponse>(jsonResult.Value);
        Assert.Equal(ancestors.Count, response.AncestorsWithRootFirst.Count());
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.EnumerateChildren("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsEmptyLists_WhenNoFilesOrContainers()
    {
        var containerId = "container";
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        storageProviderMock.Setup(sp => sp.GetWopiFiles(containerId, null, It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<IWopiFile>());
        storageProviderMock.Setup(sp => sp.GetWopiContainers(containerId, It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<IWopiFolder>());

        var result = await _controller.EnumerateChildren(containerId) as JsonResult;

        var container = Assert.IsType<Container>(result?.Value);
        Assert.Empty(container.ChildFiles);
        Assert.Empty(container.ChildContainers);
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsFilesAndContainers()
    {
        var containerId = "container";
        var fileMock = new Mock<IWopiFile>();
        fileMock.Setup(f => f.Name).Returns("file");
        fileMock.Setup(f => f.Extension).Returns("txt");
        fileMock.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.Setup(f => f.Size).Returns(123);
        fileMock.Setup(f => f.Identifier).Returns("fileId");

        var folderMock = new Mock<IWopiFolder>();
        folderMock.Setup(f => f.Name).Returns("folder");
        folderMock.Setup(f => f.Identifier).Returns("folderId");

        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        storageProviderMock.Setup(sp => sp.GetWopiFiles(containerId, null, It.IsAny<CancellationToken>())).Returns(new[] { fileMock.Object }.ToAsyncEnumerable());
        storageProviderMock.Setup(sp => sp.GetWopiContainers(containerId, It.IsAny<CancellationToken>())).Returns(new[] { folderMock.Object }.ToAsyncEnumerable());

        var result = await _controller.EnumerateChildren(containerId) as JsonResult;

        var container = Assert.IsType<Container>(result?.Value);
        Assert.Single(container.ChildFiles);
        Assert.Single(container.ChildContainers);
    }

    [Fact]
    public async Task EnumerateChildren_FiltersFilesByExtension()
    {
        var containerId = "container";
        var fileMock1 = new Mock<IWopiFile>();
        fileMock1.Setup(f => f.Name).Returns("file1");
        fileMock1.Setup(f => f.Extension).Returns("txt");
        fileMock1.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock1.Setup(f => f.Size).Returns(123);
        fileMock1.Setup(f => f.Identifier).Returns("fileId1");

        var fileMock2 = new Mock<IWopiFile>();
        fileMock2.Setup(f => f.Name).Returns("file2");
        fileMock2.Setup(f => f.Extension).Returns("doc");
        fileMock2.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock2.Setup(f => f.Size).Returns(456);
        fileMock2.Setup(f => f.Identifier).Returns("fileId2");

        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        storageProviderMock.Setup(sp => sp.GetWopiFiles(containerId, null, It.IsAny<CancellationToken>())).Returns(new[] { fileMock1.Object, fileMock2.Object }.ToAsyncEnumerable());
        storageProviderMock.Setup(sp => sp.GetWopiContainers(containerId, It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<IWopiFolder>());

        var result = await _controller.EnumerateChildren(containerId, ".txt") as JsonResult;

        var container = Assert.IsType<Container>(result?.Value);
        Assert.Single(container.ChildFiles);
        Assert.Equal("file1.txt", container.ChildFiles.First().Name);
    }
}
