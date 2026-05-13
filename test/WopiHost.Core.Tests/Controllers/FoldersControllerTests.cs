using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Controllers;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Controllers;

public class FoldersControllerTests
{
    // Hoisted to satisfy CA1861 — the Moq predicate captures this array every time the test runs;
    // a single static instance avoids per-invocation allocation and keeps the SequenceEqual stable.
    private static readonly string[] s_oneFilter = [".one"];

    private readonly Mock<IWopiStorageProvider> _storageProviderMock;
    private readonly FoldersController _controller;

    public FoldersControllerTests()
    {
        _storageProviderMock = new Mock<IWopiStorageProvider>();
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
        _controller = new FoldersController(_storageProviderMock.Object)
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
        _storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);

        var result = await _controller.CheckFolderInfo("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CheckFolderInfo_ReturnsFolderInfo_WhenFolderExists()
    {
        var folder = new Mock<IWopiFolder>();
        folder.Setup(f => f.Name).Returns("TestFolder");
        _storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => folder.Object);

        var result = await _controller.CheckFolderInfo("existing");

        var jsonResult = Assert.IsType<JsonResult<WopiCheckFolderInfo>>(result);
        var checkFolderInfo = Assert.IsType<WopiCheckFolderInfo>(jsonResult.Value);
        Assert.Equal("TestFolder", checkFolderInfo.FolderName);
    }

    [Fact]
    public async Task CheckFolderInfo_CallsOnCheckFolderInfoEvent()
    {
        // Moved here from WopiExtensionsTests.GetWopiCheckFolderInfo_CallsOnCheckFolderInfoEvent
        // when the OnCheckFolderInfo callback firing moved from the extension method to the
        // controller as part of resolving #363.
        var folder = new Mock<IWopiFolder>();
        folder.Setup(f => f.Name).Returns("MyFolder");
        _storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => folder.Object);

        var eventFired = false;
        var hostViewUrl = new Uri("https://host/view");
        var hostEditUrl = new Uri("https://host/edit");
        var closeUrl = new Uri("https://host/close");
        var fileSharingUrl = new Uri("https://host/share");
        var brandUrl = new Uri("https://brand.example.com");
        var folderUrl = new Uri("https://host/parent");
        var wopiHostOptions = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            OnCheckFolderInfo = context =>
            {
                eventFired = true;
                Assert.Equal("MyFolder", context.CheckFolderInfo.FolderName);
                Assert.NotNull(context.Folder);
                context.CheckFolderInfo.OwnerId = "owner1";
                context.CheckFolderInfo.UserCanWrite = true;
                context.CheckFolderInfo.HostViewUrl = hostViewUrl;
                context.CheckFolderInfo.HostEditUrl = hostEditUrl;
                context.CheckFolderInfo.CloseUrl = closeUrl;
                context.CheckFolderInfo.FileSharingUrl = fileSharingUrl;
                context.CheckFolderInfo.BreadcrumbBrandName = "Contoso";
                context.CheckFolderInfo.BreadcrumbBrandUrl = brandUrl;
                context.CheckFolderInfo.BreadcrumbFolderName = "ParentFolder";
                context.CheckFolderInfo.BreadcrumbFolderUrl = folderUrl;
                context.CheckFolderInfo.DisablePrint = true;
                context.CheckFolderInfo.CloseButtonClosesWindow = true;
                return Task.FromResult(context.CheckFolderInfo);
            }
        });

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope<IOptions<WopiHostOptions>>(wopiHostOptions),
            }
        };

        var result = await _controller.CheckFolderInfo("folder");

        Assert.True(eventFired);
        var jsonResult = Assert.IsType<JsonResult<WopiCheckFolderInfo>>(result);
        var checkFolderInfo = Assert.IsType<WopiCheckFolderInfo>(jsonResult.Value);
        Assert.Equal("owner1", checkFolderInfo.OwnerId);
        Assert.True(checkFolderInfo.UserCanWrite);
        Assert.Equal(hostViewUrl, checkFolderInfo.HostViewUrl);
        Assert.Equal(hostEditUrl, checkFolderInfo.HostEditUrl);
        Assert.Equal(closeUrl, checkFolderInfo.CloseUrl);
        Assert.Equal(fileSharingUrl, checkFolderInfo.FileSharingUrl);
        Assert.Equal("Contoso", checkFolderInfo.BreadcrumbBrandName);
        Assert.Equal(brandUrl, checkFolderInfo.BreadcrumbBrandUrl);
        Assert.Equal("ParentFolder", checkFolderInfo.BreadcrumbFolderName);
        Assert.Equal(folderUrl, checkFolderInfo.BreadcrumbFolderUrl);
        Assert.True(checkFolderInfo.DisablePrint);
        Assert.True(checkFolderInfo.CloseButtonClosesWindow);
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsNotFound_WhenFolderDoesNotExist()
    {
        _storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);

        var result = await _controller.EnumerateChildren("nonexistent");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsEmptyList_WhenNoFiles()
    {
        var folderId = "folder";
        _storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        _storageProviderMock
            .Setup(sp => sp.GetWopiFiles(folderId, null, It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<IWopiFile>());

        var result = await _controller.EnumerateChildren(folderId) as JsonResult;

        Assert.NotNull(result);
        var value = result.Value!;
        var childFiles = value.GetType().GetProperty("ChildFiles")?.GetValue(value) as IEnumerable<ChildFile>;
        Assert.NotNull(childFiles);
        Assert.Empty(childFiles);
    }

    [Fact]
    public async Task EnumerateChildren_ReturnsFiles()
    {
        var folderId = "folder";
        var fileMock = new Mock<IWopiFile>();
        fileMock.Setup(f => f.Name).Returns("file");
        fileMock.Setup(f => f.Extension).Returns("one");
        fileMock.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.Identifier).Returns("fileId");

        _storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        _storageProviderMock
            .Setup(sp => sp.GetWopiFiles(folderId, null, It.IsAny<CancellationToken>()))
            .Returns(new[] { fileMock.Object }.ToAsyncEnumerable());

        var result = await _controller.EnumerateChildren(folderId) as JsonResult;

        Assert.NotNull(result);
        var value = result.Value!;
        var childFiles = value.GetType().GetProperty("ChildFiles")?.GetValue(value) as IEnumerable<ChildFile>;
        Assert.NotNull(childFiles);
        Assert.Single(childFiles);
    }

    [Fact]
    public async Task EnumerateChildren_ForwardsExtensionFilterToProvider()
    {
        // The controller forwards X-WOPI-FileExtensionFilterList — parsed into a typed list —
        // to the provider; the provider filters at the storage layer. This test pins both
        // halves: the parsed [".one"] reaches the provider, and the controller materializes
        // whatever it returned without any in-memory post-filter.
        var folderId = "folder";
        var fileMock1 = new Mock<IWopiFile>();
        fileMock1.Setup(f => f.Name).Returns("notebook1");
        fileMock1.Setup(f => f.Extension).Returns("one");
        fileMock1.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock1.Setup(f => f.Length).Returns(1024);
        fileMock1.Setup(f => f.Identifier).Returns("fileId1");

        _storageProviderMock
            .Setup(sp => sp.GetWopiResource<IWopiFolder>(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IWopiFolder>().Object);
        _storageProviderMock
            .Setup(sp => sp.GetWopiFiles(
                folderId,
                It.Is<IReadOnlyCollection<string>?>(exts => exts != null && exts.SequenceEqual(s_oneFilter)),
                It.IsAny<CancellationToken>()))
            .Returns(new[] { fileMock1.Object }.ToAsyncEnumerable());

        var result = await _controller.EnumerateChildren(folderId, ".one") as JsonResult;

        Assert.NotNull(result);
        var value = result.Value!;
        var childFiles = (value.GetType().GetProperty("ChildFiles")?.GetValue(value) as IEnumerable<ChildFile>)?.ToList();
        Assert.NotNull(childFiles);
        Assert.Single(childFiles);
        Assert.Equal("notebook1.one", childFiles[0].Name);
    }
}
