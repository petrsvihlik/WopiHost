using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Controllers;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Controllers;

public class FoldersControllerTests
{
    private readonly Mock<IWopiStorageProvider> storageProviderMock;
    private readonly FoldersController _controller;

    public FoldersControllerTests()
    {
        storageProviderMock = new Mock<IWopiStorageProvider>();
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
                    ServiceScopeFactory = TestUtils.CreateServiceScope(new Mock<IAuthenticationService>().Object),
                }
            });
        _controller = new FoldersController(storageProviderMock.Object)
        {
            Url = url.Object,
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
                {
                    ServiceScopeFactory = TestUtils.CreateServiceScope()
                }
            }
        };
    }

    [Fact]
    public async Task CheckFolderInfo_ReturnsNotFound_WhenFolderDoesNotExist()
    {
        // Arrange
        storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);

        // Act
        var result = await _controller.CheckFolderInfo("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CheckFolderInfo_ReturnsFolderInfo_WhenFolderExists()
    {
        // Arrange
        var folder = new Mock<IWopiFolder>();
        folder.Setup(f => f.Name).Returns("TestFolder");
        storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => folder.Object);

        // Act
        var result = await _controller.CheckFolderInfo("existing");

        // Assert
        var jsonResult = Assert.IsType<JsonResult<WopiCheckFolderInfo>>(result);
        var checkFolderInfo = Assert.IsType<WopiCheckFolderInfo>(jsonResult.Value);
        Assert.Equal("TestFolder", checkFolderInfo.FolderName);
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsNotFound_WhenFolderDoesNotExist()
    {
        // Arrange
        storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);

        // Act
        var result = await _controller.EnumerateChildren("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsEmptyList_WhenNoFiles()
    {
        // Arrange
        var folderId = "folder";
        storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        storageProviderMock
            .Setup(sp => sp.GetWopiFiles(folderId, null, It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<IWopiFile>());

        // Act
        var result = await _controller.EnumerateChildren(folderId) as JsonResult;

        // Assert
        Assert.NotNull(result);
        var value = result.Value!;
        var childFiles = value.GetType().GetProperty("ChildFiles")?.GetValue(value) as IEnumerable<ChildFile>;
        Assert.NotNull(childFiles);
        Assert.Empty(childFiles);
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsFiles()
    {
        // Arrange
        var folderId = "folder";
        var fileMock = new Mock<IWopiFile>();
        fileMock.Setup(f => f.Name).Returns("file");
        fileMock.Setup(f => f.Extension).Returns("one");
        fileMock.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.Setup(f => f.Size).Returns(1024);
        fileMock.Setup(f => f.Identifier).Returns("fileId");

        storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        storageProviderMock
            .Setup(sp => sp.GetWopiFiles(folderId, null, It.IsAny<CancellationToken>()))
            .Returns(new[] { fileMock.Object }.ToAsyncEnumerable());

        // Act
        var result = await _controller.EnumerateChildren(folderId) as JsonResult;

        // Assert
        Assert.NotNull(result);
        var value = result.Value!;
        var childFiles = value.GetType().GetProperty("ChildFiles")?.GetValue(value) as IEnumerable<ChildFile>;
        Assert.NotNull(childFiles);
        Assert.Single(childFiles);
    }

    [Fact]
    public async Task EnumerateChildren_FiltersFilesByExtension()
    {
        // Arrange
        var folderId = "folder";
        var fileMock1 = new Mock<IWopiFile>();
        fileMock1.Setup(f => f.Name).Returns("notebook1");
        fileMock1.Setup(f => f.Extension).Returns("one");
        fileMock1.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock1.Setup(f => f.Size).Returns(1024);
        fileMock1.Setup(f => f.Identifier).Returns("fileId1");

        var fileMock2 = new Mock<IWopiFile>();
        fileMock2.Setup(f => f.Name).Returns("document");
        fileMock2.Setup(f => f.Extension).Returns("docx");
        fileMock2.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock2.Setup(f => f.Size).Returns(512);
        fileMock2.Setup(f => f.Identifier).Returns("fileId2");

        storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        storageProviderMock
            .Setup(sp => sp.GetWopiFiles(folderId, null, It.IsAny<CancellationToken>()))
            .Returns(new[] { fileMock1.Object, fileMock2.Object }.ToAsyncEnumerable());

        // Act
        var result = await _controller.EnumerateChildren(folderId, ".one") as JsonResult;

        // Assert
        Assert.NotNull(result);
        var value = result.Value!;
        var childFiles = (value.GetType().GetProperty("ChildFiles")?.GetValue(value) as IEnumerable<ChildFile>)?.ToList();
        Assert.NotNull(childFiles);
        Assert.Single(childFiles);
        Assert.Equal("notebook1.one", childFiles[0].Name);
    }
}
