using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    private readonly Mock<IOptions<WopiHostOptions>> _mockWopiHostOptions;
    private readonly Mock<IWopiWritableStorageProvider> _writableStorageProviderMock;
    private readonly ContainersController _controller;

    public ContainersControllerTests()
    {
        _storageProviderMock = new Mock<IWopiStorageProvider>();
        _mockWopiHostOptions = new Mock<IOptions<WopiHostOptions>>();
        _mockWopiHostOptions.Setup(o => o.Value).Returns(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            OnCheckContainerInfo = context => Task.FromResult(context.CheckContainerInfo)
        });
        _writableStorageProviderMock = new Mock<IWopiWritableStorageProvider>();
        var url = new Mock<IUrlHelper>();
        url
            .Setup(_ => _.RouteUrl(It.IsAny<UrlRouteContext>()))
            .Returns("https://localhost");
        url
            .Setup(_ => _.ActionContext)
            .Returns(new ActionContext
            {
                HttpContext = new DefaultHttpContext()
                {
                    ServiceScopeFactory = CreateServiceScope(new Mock<IAuthenticationService>().Object).Object,
                }
            });
        _controller = new ContainersController(
            _storageProviderMock.Object,
            _mockWopiHostOptions.Object,
            _writableStorageProviderMock.Object)
        {
            Url = url.Object,
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    public static Mock<IServiceScopeFactory> CreateServiceScope<T1>(T1 instance1)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(x => x.GetService(typeof(T1)))
            .Returns(instance1);

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        serviceScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(serviceScope.Object);

        return serviceScopeFactory;
    }

    [Fact]
    public async Task CheckContainerInfo_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        // Arrange
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns((IWopiFolder)null!);

        // Act
        var result = await _controller.CheckContainerInfo("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CheckContainerInfo_ReturnsContainerInfo_WhenContainerExists()
    {
        // Arrange
        var container = new Mock<IWopiFolder>();
        container.Setup(c => c.Name).Returns("TestContainer");
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns(container.Object);
        

        // Act
        var result = await _controller.CheckContainerInfo("existing");

        // Assert
        var jsonResult = Assert.IsType<JsonResult<WopiCheckContainerInfo>>(result);
        var checkContainerInfo = Assert.IsType<WopiCheckContainerInfo>(jsonResult.Value);
        Assert.Equal("TestContainer", checkContainerInfo.Name);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsNotImplemented_WhenWritableStorageProviderIsNull()
    {
        // Arrange
        var controller = new ContainersController(
            _storageProviderMock.Object,
            _mockWopiHostOptions.Object,
            null);

        // Act
        var result = await controller.CreateChildContainer("id");

        // Assert
        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        // Arrange
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns((IWopiFolder)null!);

        // Act
        var result = await _controller.CreateChildContainer("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsNotImplemented_WhenBothHeadersArePresent()
    {
        // Arrange
        var containerId = "existing-container";
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(new Mock<IWopiFolder>().Object);

        // Act
        var result = await _controller.CreateChildContainer(containerId, UtfString.FromDecoded("suggestedTarget"), UtfString.FromDecoded("relativeTarget"));

        // Assert
        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsNotImplemented_WhenBothHeadersAreMissing()
    {
        // Arrange
        var containerId = "existing-container";
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(new Mock<IWopiFolder>().Object);

        // Act
        var result = await _controller.CreateChildContainer(containerId);

        // Assert
        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsBadRequest_WhenNameIsInvalid()
    {
        // Arrange
        var containerId = "existing-container";
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName(WopiResourceType.Container, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _controller.CreateChildContainer(containerId, UtfString.FromDecoded("suggestedTarget"));

        // Assert
        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsConflict_WhenContainerAlreadyExists()
    {
        // Arrange
        var containerId = "existing-container";
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName(WopiResourceType.Container, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storageProviderMock.Setup(sp => sp.GetWopiResourceByName(WopiResourceType.Container, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IWopiResource>().Object);

        // Act
        var result = await _controller.CreateChildContainer(containerId, relativeTarget: UtfString.FromDecoded("relativeTarget"));

        // Assert
        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsInternalServerError_WhenNewFolderIsNull()
    {
        // Arrange
        var containerId = "existing-container";
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName(WopiResourceType.Container, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storageProviderMock.Setup(sp => sp.GetWopiResourceByName(WopiResourceType.Container, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((IWopiResource)null!);

        // Act
        var result = await _controller.CreateChildContainer(containerId, relativeTarget: UtfString.FromDecoded("relativeTarget"));

        // Assert
        Assert.IsType<InternalServerErrorResult>(result);
    }

    [Fact]
    public async Task CreateChildContainer_ReturnsJsonResult_WhenSuccessful()
    {
        // Arrange
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        container.Setup(c => c.Name).Returns("container");
        container.Setup(c => c.Identifier).Returns(containerId);
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(container.Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName(WopiResourceType.Container, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _storageProviderMock.Setup(sp => sp.GetWopiResourceByName(WopiResourceType.Container, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((IWopiResource)null!);
        _writableStorageProviderMock.Setup(_ => _.CreateWopiChildResource(WopiResourceType.Container, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(container.Object);

        // Act
        var result = await _controller.CreateChildContainer(containerId, relativeTarget: UtfString.FromDecoded("relativeTarget"));

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<CreateChildContainerResponse>(jsonResult.Value);
    }

    [Fact]
    public async Task CreateChildContainer_SuggestedTarget_ReturnsJsonResult_WhenSuccessful()
    {
        // Arrange
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        container.Setup(c => c.Name).Returns("container");
        container.Setup(c => c.Identifier).Returns(containerId);
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(container.Object);
        _writableStorageProviderMock.Setup(wsp => wsp.CheckValidName(WopiResourceType.Container, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writableStorageProviderMock.Setup(wsp => wsp.GetSuggestedName(WopiResourceType.Container, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("suggestedTarget");
        _writableStorageProviderMock.Setup(_ => _.CreateWopiChildResource(WopiResourceType.Container, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(container.Object);

        // Act
        var result = await _controller.CreateChildContainer(containerId, suggestedTarget: UtfString.FromDecoded("suggestedTarget"));

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<CreateChildContainerResponse>(jsonResult.Value);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsNotImplemented_WhenWritableStorageProviderIsNull()
    {
        // Arrange
        var controller = new ContainersController(
            _storageProviderMock.Object,
            _mockWopiHostOptions.Object,
            null);

        // Act
        var result = await controller.DeleteContainer("id");

        // Assert
        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        // Arrange
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns((IWopiFolder)null!);

        // Act
        var result = await _controller.DeleteContainer("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsOk_WhenContainerExist()
    {
        // Arrange
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(container.Object);
        _writableStorageProviderMock.Setup(_ => _.DeleteWopiResource(WopiResourceType.Container, containerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteContainer(containerId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsInternalServerError_WhenDeleteFails()
    {
        // Arrange
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(container.Object);
        _writableStorageProviderMock.Setup(_ => _.DeleteWopiResource(WopiResourceType.Container, containerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteContainer(containerId);

        // Assert
        Assert.IsType<InternalServerErrorResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsNotFound_WhenDeleteFailsWithDirectoryNotFoundException()
    {
        // Arrange
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(container.Object);
        _writableStorageProviderMock.Setup(_ => _.DeleteWopiResource(WopiResourceType.Container, containerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DirectoryNotFoundException());

        // Act
        var result = await _controller.DeleteContainer(containerId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteContainer_ReturnsConflict_WhenDeleteFailsWithInvalidOperationException()
    {
        // Arrange
        var containerId = "existing-container";
        var container = new Mock<IWopiFolder>();
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(container.Object);
        _writableStorageProviderMock.Setup(_ => _.DeleteWopiResource(WopiResourceType.Container, containerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        // Act
        var result = await _controller.DeleteContainer(containerId);

        // Assert
        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsNotImplemented_WhenWritableStorageProviderIsNull()
    {
        // Arrange
        var controller = new ContainersController(
            _storageProviderMock.Object,
            _mockWopiHostOptions.Object,
            null);

        // Act
        var result = await controller.RenameContainer("id", new UtfString());

        // Assert
        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsNotFound_WhenContainerNotFound()
    {
        // Arrange
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns((IWopiFolder)null!);

        // Act
        var result = await _controller.RenameContainer("id", new UtfString());

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsBadRequest_WhenRequestedNameIsIllegal()
    {
        // Arrange
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource(It.IsAny<WopiResourceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid name", "requestedName"));

        // Act
        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("illegalName"));

        // Assert
        Assert.IsType<BadRequestResult>(result);
        Assert.Equal("Specified name is illegal", _controller.Response.Headers[WopiHeaders.INVALID_CONTAINER_NAME]);
    }

    [Fact]
    public async Task RenameContainer_ReturnsConflict_WhenRequestedNameAlreadyExists()
    {
        // Arrange
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource(It.IsAny<WopiResourceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        // Act
        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("existingName"));

        // Assert
        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsNotFound_WhenDirectoryNotFoundException()
    {
        // Arrange
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource(It.IsAny<WopiResourceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DirectoryNotFoundException());

        // Act
        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("existingName"));

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsInternalServerError_WhenUnexpectedErrorOccurs()
    {
        // Arrange
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns(new Mock<IWopiFolder>().Object);
        _writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource(It.IsAny<WopiResourceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());

        // Act
        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("newName"));

        // Assert
        Assert.IsType<InternalServerErrorResult>(result);
    }

    [Fact]
    public async Task RenameContainer_ReturnsJsonResult_WhenRenameIsSuccessful()
    {
        // Arrange
        var containerMock = new Mock<IWopiFolder>();
        containerMock.SetupGet(c => c.Name).Returns("newName");
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns(containerMock.Object);
        _writableStorageProviderMock.Setup(wsp => wsp.RenameWopiResource(It.IsAny<WopiResourceType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.RenameContainer("containerId", UtfString.FromDecoded("newName"));

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
    }

    [Fact]
    public void GetEcosystem_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        // Arrange
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns((IWopiFolder)null!);

        // Act
        var result = _controller.GetEcosystem("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetEcosystem_ReturnsLink()
    {
        // Arrange
        var containerId = "existing-container";
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(new Mock<IWopiFolder>().Object);

        // Act
        var result = _controller.GetEcosystem(containerId);

        // Assert
        var jsonResult = Assert.IsType<JsonResult<UrlResponse>>(result);
        var response = Assert.IsType<UrlResponse>(jsonResult.Value);
    }

    [Fact]
    public async Task EnumerateAncestors_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        // Arrange
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns((IWopiFolder)null!);

        // Act
        var result = await _controller.EnumerateAncestors("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EnumerateAncestors_ReturnsAncestors_WhenContainerExists()
    {
        // Arrange
        var containerId = "existing-container";
        var ancestors = new ReadOnlyCollection<IWopiFolder>(new List<IWopiFolder>
        {
            new Mock<IWopiFolder>().Object,
            new Mock<IWopiFolder>().Object
        });
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(new Mock<IWopiFolder>().Object);
        _storageProviderMock.Setup(sp => sp.GetAncestors(WopiResourceType.Container, containerId, It.IsAny<CancellationToken>())).ReturnsAsync(ancestors);

        // Act
        var result = await _controller.EnumerateAncestors(containerId);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var response = Assert.IsType<EnumerateAncestorsResponse>(jsonResult.Value);
        Assert.Equal(ancestors.Count, response.AncestorsWithRootFirst.Count());
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsNotFound_WhenContainerDoesNotExist()
    {
        // Arrange
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(It.IsAny<string>())).Returns((IWopiFolder)null!);

        // Act
        var result = await _controller.EnumerateChildren("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsEmptyLists_WhenNoFilesOrContainers()
    {
        // Arrange
        var containerId = "container";
        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(new Mock<IWopiFolder>().Object);
        _storageProviderMock.Setup(sp => sp.GetWopiFiles(containerId, null, It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<IWopiFile>());
        _storageProviderMock.Setup(sp => sp.GetWopiContainers(containerId, It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<IWopiFolder>());

        // Act
        var result = await _controller.EnumerateChildren(containerId) as JsonResult;

        // Assert
        var container = Assert.IsType<Container>(result?.Value);
        Assert.Empty(container.ChildFiles);
        Assert.Empty(container.ChildContainers);
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsFilesAndContainers()
    {
        // Arrange
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

        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(new Mock<IWopiFolder>().Object);
        _storageProviderMock.Setup(sp => sp.GetWopiFiles(containerId, null, It.IsAny<CancellationToken>())).Returns(new[] { fileMock.Object }.ToAsyncEnumerable());
        _storageProviderMock.Setup(sp => sp.GetWopiContainers(containerId, It.IsAny<CancellationToken>())).Returns(new[] { folderMock.Object }.ToAsyncEnumerable());

        // Act
        var result = await _controller.EnumerateChildren(containerId) as JsonResult;

        // Assert
        var container = Assert.IsType<Container>(result?.Value);
        Assert.Single(container.ChildFiles);
        Assert.Single(container.ChildContainers);
    }

    [Fact]
    public async Task EnumerateChildren_FiltersFilesByExtension()
    {
        // Arrange
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

        _storageProviderMock.Setup(sp => sp.GetWopiContainer(containerId)).Returns(new Mock<IWopiFolder>().Object);
        _storageProviderMock.Setup(sp => sp.GetWopiFiles(containerId, null, It.IsAny<CancellationToken>())).Returns(new[] { fileMock1.Object, fileMock2.Object }.ToAsyncEnumerable());
        _storageProviderMock.Setup(sp => sp.GetWopiContainers(containerId, It.IsAny<CancellationToken>())).Returns(AsyncEnumerable.Empty<IWopiFolder>());

        // Act
        var result = await _controller.EnumerateChildren(containerId, ".txt") as JsonResult;

        // Assert
        var container = Assert.IsType<Container>(result?.Value);
        Assert.Single(container.ChildFiles);
        Assert.Equal("file1.txt", container.ChildFiles.First().Name);
    }
}
