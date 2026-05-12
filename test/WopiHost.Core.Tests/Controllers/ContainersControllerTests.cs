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
    private readonly Mock<IWopiStorageProvider> _storageProviderMock;
    private readonly Mock<IWopiLockProvider> _lockProviderMock;
    private readonly Mock<IWopiWritableStorageProvider> _writableStorageProviderMock;
    private readonly Mock<IWopiPermissionProvider> _permissionProviderMock;
    private readonly Mock<IWopiAccessTokenService> _accessTokenServiceMock;
    private readonly Mock<IUrlHelper> _urlMock;
    private readonly ContainersController _controller;

    public ContainersControllerTests()
    {
        _storageProviderMock = new Mock<IWopiStorageProvider>();
        _lockProviderMock = new Mock<IWopiLockProvider>();
        _writableStorageProviderMock = new Mock<IWopiWritableStorageProvider>();
        _permissionProviderMock = new Mock<IWopiPermissionProvider>();
        _permissionProviderMock
            .Setup(_ => _.GetContainerPermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFolder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiContainerPermissions.UserCanCreateChildContainer | WopiContainerPermissions.UserCanCreateChildFile | WopiContainerPermissions.UserCanDelete | WopiContainerPermissions.UserCanRename);
        _accessTokenServiceMock = new Mock<IWopiAccessTokenService>();
        _accessTokenServiceMock
            .Setup(s => s.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiAccessToken("FRESH-TOKEN", DateTimeOffset.UtcNow.AddMinutes(10)));
        _urlMock = new Mock<IUrlHelper>();
        _urlMock
            .Setup(_ => _.RouteUrl(It.IsAny<UrlRouteContext>()))
            .Returns("https://localhost");
        _urlMock
            .Setup(_ => _.ActionContext)
            .Returns(new ActionContext
            {
                HttpContext = new DefaultHttpContext()
                {
                    ServiceScopeFactory = TestUtils.CreateServiceScope(new Mock<IAuthenticationService>().Object),
                }
            });
        _controller = new ContainersController(
            _storageProviderMock.Object,
            _lockProviderMock.Object,
            _writableStorageProviderMock.Object)
        {
            Url = _urlMock.Object,
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
                {
                    ServiceScopeFactory = TestUtils.CreateServiceScope(_permissionProviderMock.Object),
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
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.CheckContainerInfo("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CheckContainerInfo_ReturnsContainerInfo_WhenContainerExists()
    {
        var container = new Mock<IWopiFolder>();
        container.Setup(c => c.Name).Returns("TestContainer");
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => container.Object);

        var result = await _controller.CheckContainerInfo("existing");

        var jsonResult = Assert.IsType<JsonResult<WopiCheckContainerInfo>>(result);
        var checkContainerInfo = Assert.IsType<WopiCheckContainerInfo>(jsonResult.Value);
        Assert.Equal("TestContainer", checkContainerInfo.Name);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.CreateChildContainer("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsNotImplemented_WhenBothHeadersArePresent()
    {
        var containerId = "existing-container";
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);

        var result = await _controller.CreateChildContainer(containerId, UtfString.FromDecoded("suggestedTarget"), UtfString.FromDecoded("relativeTarget"));

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsNotImplemented_WhenBothHeadersAreMissing()
    {
        var containerId = "existing-container";
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);

        var result = await _controller.CreateChildContainer(containerId);

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsBadRequest_WhenNameIsInvalid()
    {
        var containerId = "existing-container";
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.CreateChildContainer(containerId, UtfString.FromDecoded("suggestedTarget"));

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsConflict_WhenContainerAlreadyExists()
    {
        var containerId = "existing-container";
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storageProviderMock.Setup(sp => sp.GetWopiResourceByName<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);

        var result = await _controller.CreateChildContainer(containerId, relativeTarget: UtfString.FromDecoded("relativeTarget"));

        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsInternalServerError_WhenNewFolderIsNull()
    {
        var containerId = "existing-container";
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storageProviderMock.Setup(sp => sp.GetWopiResourceByName<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

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
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storageProviderMock.Setup(sp => sp.GetWopiResourceByName<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);
        _writableStorageProviderMock.Setup(_ => _.CreateWopiChildResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writableStorageProviderMock.Setup(wsp => wsp.GetSuggestedName<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("suggestedTarget");
        _writableStorageProviderMock.Setup(_ => _.CreateWopiChildResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(container.Object);

        var result = await _controller.CreateChildContainer(containerId, suggestedTarget: UtfString.FromDecoded("suggestedTarget"));

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<CreateChildContainerResponse>(jsonResult.Value);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);

        var result = await _controller.CreateChildFile("containerId");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsNotImplemented_WhenBothHeadersArePresent()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);

        var result = await _controller.CreateChildFile("containerId", UtfString.FromDecoded("suggestedTarget"), UtfString.FromDecoded("relativeTarget"));

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsNotImplemented_WhenBothHeadersAreMissing()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);

        var result = await _controller.CreateChildFile("containerId");

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsBadRequest_WhenNameIsInvalid()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.CreateChildFile("containerId", UtfString.FromDecoded("relativeTarget"));

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsConflict_WhenFileAlreadyExistsAndOverwriteIsFalse()
    {
        var existingFile = new Mock<IWopiFile>().Object;
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        _storageProviderMock.Setup(sp => sp.GetWopiResourceByName<IWopiFile>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.CreateChildFile("containerId", relativeTarget: UtfString.FromDecoded("relativeTarget"), overwriteRelativeTarget: false);

        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task CreateChildFile_ReturnsLockMismatch_WhenFileIsLocked()
    {
        var existingFile = new Mock<IWopiFile>().Object;
        var lockInfo = new WopiLockInfo { FileId = "any", LockId = "lockId" };
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        _storageProviderMock.Setup(sp => sp.GetWopiResourceByName<IWopiFile>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _lockProviderMock.Setup(lp => lp.GetLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
        // Stub Checksum so GetEncodedSha256 takes the early-return path; avoids the unmocked
        // OpenReadAsync call inside CheckFileInfo composition.
        fileMock.SetupGet(f => f.Checksum).Returns(new ReadOnlyMemory<byte>([0]));
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock.Setup(wsp => wsp.CreateWopiChildResource<IWopiFile>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        var result = await _controller.CreateChildFile("containerId", relativeTarget: UtfString.FromDecoded("relativeTarget"));

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<ChildFile>(jsonResult.Value);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.DeleteContainer("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsOk_WhenContainerExist()
    {
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        _writableStorageProviderMock.Setup(_ => _.DeleteWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DeleteContainer(containerId);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsInternalServerError_WhenDeleteFails()
    {
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        _writableStorageProviderMock.Setup(_ => _.DeleteWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.DeleteContainer(containerId);

        Assert.IsType<InternalServerErrorResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsNotFound_WhenDeleteFailsWithDirectoryNotFoundException()
    {
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        _writableStorageProviderMock.Setup(_ => _.DeleteWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DirectoryNotFoundException());

        var result = await _controller.DeleteContainer(containerId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsConflict_WhenDeleteFailsWithInvalidOperationException()
    {
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        _writableStorageProviderMock.Setup(_ => _.DeleteWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        var result = await _controller.DeleteContainer(containerId);

        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsNotFound_WhenContainerNotFound()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.RenameContainer("id", new UtfString());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsBadRequest_WhenRequestedNameIsIllegal()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("illegalName"));

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal("Specified name is illegal", _controller.Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME]);
    }

    [Fact]
    public async Task RenameContainer_ReturnsConflict_WhenRequestedNameAlreadyExists()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("existingName"));

        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsNotFound_WhenDirectoryNotFoundException()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DirectoryNotFoundException());

        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("existingName"));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsInternalServerError_WhenUnexpectedErrorOccurs()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());

        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("newName"));

        Assert.IsType<InternalServerErrorResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsJsonResult_WhenRenameIsSuccessful()
    {
        var container = new Mock<IWopiFolder>();
        container.SetupGet(c => c.Name).Returns("newName");
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(container.Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("newName"));

        var jsonResult = Assert.IsType<JsonResult>(result);
    }

    [Fact]
    public async Task GetEcosystem_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.GetEcosystem("nonexistent", _accessTokenServiceMock.Object);

        Assert.IsType<NotFoundResult>(result);
        _accessTokenServiceMock.Verify(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetEcosystem_ReturnsLink()
    {
        var containerId = "existing-container";
        var folderMock = new Mock<IWopiFolder>();
        folderMock.SetupGet(f => f.Identifier).Returns(containerId);
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(folderMock.Object);

        var result = await _controller.GetEcosystem(containerId, _accessTokenServiceMock.Object);

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
        _storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folderMock.Object);

        UrlRouteContext? captured = null;
        _urlMock
            .Setup(_ => _.RouteUrl(It.IsAny<UrlRouteContext>()))
            .Callback<UrlRouteContext>(rc => captured = rc)
            .Returns("https://localhost/wopi/ecosystem");

        await _controller.GetEcosystem(containerId, _accessTokenServiceMock.Object);

        _accessTokenServiceMock.Verify(t => t.IssueAsync(
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
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

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
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        _storageProviderMock.Setup(sp => sp.GetAncestors<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(ancestors);

        var result = await _controller.EnumerateAncestors(containerId);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var response = Assert.IsType<EnumerateAncestorsResponse>(jsonResult.Value);
        Assert.Equal(ancestors.Count, response.AncestorsWithRootFirst.Count());
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.EnumerateChildren("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsEmptyLists_WhenNoFilesOrContainers()
    {
        var containerId = "container";
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        _storageProviderMock.Setup(sp => sp.GetWopiFiles(containerId, null, It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<IWopiFile>());
        _storageProviderMock.Setup(sp => sp.GetWopiContainers(containerId, It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<IWopiFolder>());

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
        fileMock.Setup(f => f.Length).Returns(123);
        fileMock.Setup(f => f.Identifier).Returns("fileId");

        var folderMock = new Mock<IWopiFolder>();
        folderMock.Setup(f => f.Name).Returns("folder");
        folderMock.Setup(f => f.Identifier).Returns("folderId");

        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        _storageProviderMock.Setup(sp => sp.GetWopiFiles(containerId, null, It.IsAny<CancellationToken>())).Returns(new[] { fileMock.Object }.ToAsyncEnumerable());
        _storageProviderMock.Setup(sp => sp.GetWopiContainers(containerId, It.IsAny<CancellationToken>())).Returns(new[] { folderMock.Object }.ToAsyncEnumerable());

        var result = await _controller.EnumerateChildren(containerId) as JsonResult;

        var container = Assert.IsType<Container>(result?.Value);
        Assert.Single(container.ChildFiles);
        Assert.Single(container.ChildContainers);
    }

    [Fact]
    public async Task EnumerateChildren_ForwardsExtensionFilterToProvider()
    {
        // The controller's only job here is to parse X-WOPI-FileExtensionFilterList into a
        // typed list and hand it to the provider — the provider does the actual filtering at
        // the storage layer. This test pins both halves of that contract:
        //   1) the controller forwards the parsed [".txt"] (not the raw header) to the provider;
        //   2) the controller materializes whatever the provider returned, no in-memory filter.
        // The mock returns only file1 (the .txt match) — what a real filtering provider would.
        var containerId = "container";
        var fileMock1 = new Mock<IWopiFile>();
        fileMock1.Setup(f => f.Name).Returns("file1");
        fileMock1.Setup(f => f.Extension).Returns("txt");
        fileMock1.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock1.Setup(f => f.Length).Returns(123);
        fileMock1.Setup(f => f.Identifier).Returns("fileId1");

        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFolder>(containerId, It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiFolder>().Object);
        _storageProviderMock
            .Setup(sp => sp.GetWopiFiles(
                containerId,
                It.Is<IReadOnlyCollection<string>?>(exts => exts != null && exts.SequenceEqual(new[] { ".txt" })),
                It.IsAny<CancellationToken>()))
            .Returns(new[] { fileMock1.Object }.ToAsyncEnumerable());
        _storageProviderMock.Setup(sp => sp.GetWopiContainers(containerId, It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<IWopiFolder>());

        var result = await _controller.EnumerateChildren(containerId, ".txt") as JsonResult;

        var container = Assert.IsType<Container>(result?.Value);
        Assert.Single(container.ChildFiles);
        Assert.Equal("file1.txt", container.ChildFiles.First().Name);
    }
}
