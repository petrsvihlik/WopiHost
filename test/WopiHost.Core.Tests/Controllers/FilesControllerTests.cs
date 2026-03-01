using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Controllers;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Controllers;

public class FilesControllerTests
{
    private readonly Mock<IWopiStorageProvider> storageProviderMock;
    private readonly Mock<IWopiWritableStorageProvider> writableStorageProviderMock;
    private readonly Mock<IWopiSecurityHandler> securityHandlerMock;
    private readonly Mock<IOptions<WopiHostOptions>> wopiHostOptionsMock;
    private readonly IMemoryCache memoryCache;
    private readonly Mock<IWopiLockProvider> lockProviderMock;
    private readonly Mock<IUrlHelper> urlMock;
    private FilesController controller;

    public FilesControllerTests()
    {
        storageProviderMock = new Mock<IWopiStorageProvider>();
        writableStorageProviderMock = new Mock<IWopiWritableStorageProvider>();
        securityHandlerMock = new Mock<IWopiSecurityHandler>();
        wopiHostOptionsMock = new Mock<IOptions<WopiHostOptions>>();
        wopiHostOptionsMock
            .SetupGet(o => o.Value)
            .Returns(new WopiHostOptions()
            {
                StorageProviderAssemblyName = "test",
                LockProviderAssemblyName = "test",
                OnCheckFileInfo = o => Task.FromResult(o.CheckFileInfo),
                ClientUrl = new Uri("http://localhost:5000"),
            });
        memoryCache = new MemoryCache(new MemoryCacheOptions());
        lockProviderMock = new Mock<IWopiLockProvider>();

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

        controller = new FilesController(
            storageProviderMock.Object,
            memoryCache,
            writableStorageProviderMock.Object,
            lockProviderMock.Object)
        {
            Url = urlMock.Object,
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    /// <summary>
    /// Creates a fully-configured file mock with all properties and streams set up.
    /// </summary>
    private Mock<IWopiFile> CreateFileMock(string fileId = "testFileId", long size = 1024, string? version = "1.0")
    {
        var fileMock = new Mock<IWopiFile>();
        fileMock.SetupGet(f => f.Owner).Returns("ownerId");
        fileMock.SetupGet(f => f.Version).Returns(version);
        fileMock.SetupGet(f => f.Name).Returns("test");
        fileMock.SetupGet(f => f.Extension).Returns("txt");
        fileMock.SetupGet(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.SetupGet(f => f.Length).Returns(size);
        fileMock.SetupGet(f => f.Size).Returns(size);
        fileMock.SetupGet(f => f.Exists).Returns(size > 0);
        fileMock.SetupGet(f => f.Identifier).Returns(fileId);
        fileMock.Setup(f => f.GetReadStream(It.IsAny<CancellationToken>())).ReturnsAsync(new System.IO.MemoryStream());
        fileMock.Setup(f => f.GetWriteStream(It.IsAny<CancellationToken>())).ReturnsAsync(new System.IO.MemoryStream());
        return fileMock;
    }

    #region CheckFileInfo

    [Fact]
    public async Task GetCheckFileInfo_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        var fileId = "testFileId";
        storageProviderMock.Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        // Act
        var result = await controller.CheckFileInfo(fileId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetCheckFileInfo_Success_ReturnsFileInfoForAnonymous()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = new Mock<IWopiFile>();
        fileMock.SetupGet(f => f.Owner).Returns("ownerId");
        fileMock.SetupGet(f => f.Version).Returns("1.0");
        fileMock.SetupGet(f => f.Name).Returns("test");
        fileMock.SetupGet(f => f.Extension).Returns("txt");
        fileMock.SetupGet(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.SetupGet(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.GetReadStream(It.IsAny<CancellationToken>())).ReturnsAsync(new System.IO.MemoryStream());
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fileMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope(securityHandlerMock.Object),
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                    [
                        new(ClaimTypes.NameIdentifier, "userId"),
                        new(ClaimTypes.Name, "test")
                    ])),
            }
        };

        // Act
        var result = await controller.CheckFileInfo(fileId);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.NotNull(contentResult.Content);
        Assert.Equal(StatusCodes.Status200OK, contentResult.StatusCode);
        Assert.Equal(MediaTypeNames.Application.Json, contentResult.ContentType);
        var resultContent = JsonSerializer.Deserialize<WopiCheckFileInfo>(contentResult.Content);
        Assert.NotNull(resultContent);
        Assert.Equal(fileMock.Object.Owner, resultContent.OwnerId);
        Assert.Equal(fileMock.Object.Version, resultContent.Version);
        Assert.Equal("." + fileMock.Object.Extension, resultContent.FileExtension);
        Assert.Equal(fileMock.Object.Name + "." + fileMock.Object.Extension, resultContent.BaseFileName);
        Assert.Equal(fileMock.Object.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture), resultContent.LastModifiedTime);
        Assert.True(resultContent.IsAnonymousUser);
    }

    [Fact]
    public async Task GetCheckFileInfo_Success_ReturnsFileInfoWithAuthenticatedUser()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = new Mock<IWopiFile>();
        fileMock.SetupGet(f => f.Owner).Returns("ownerId");
        fileMock.SetupGet(f => f.Version).Returns("1.0");
        fileMock.SetupGet(f => f.Name).Returns("test");
        fileMock.SetupGet(f => f.Extension).Returns("txt");
        fileMock.SetupGet(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.SetupGet(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.GetReadStream(It.IsAny<CancellationToken>())).ReturnsAsync(new System.IO.MemoryStream());

        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fileMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope(securityHandlerMock.Object),
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "nameId"),
                        new Claim(ClaimTypes.Name, "testUser")
                    ], "TestAuthentication"))
            }
        };

        // Act
        var result = await controller.CheckFileInfo(fileId);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.NotNull(contentResult.Content);
        Assert.Equal(StatusCodes.Status200OK, contentResult.StatusCode);
        Assert.Equal(MediaTypeNames.Application.Json, contentResult.ContentType);
        var resultContent = JsonSerializer.Deserialize<WopiCheckFileInfo>(contentResult.Content);
        Assert.NotNull(resultContent);
        Assert.Equal(fileMock.Object.Owner, resultContent.OwnerId);
        Assert.Equal(fileMock.Object.Version, resultContent.Version);
        Assert.Equal("." + fileMock.Object.Extension, resultContent.FileExtension);
        Assert.Equal(fileMock.Object.Name + "." + fileMock.Object.Extension, resultContent.BaseFileName);
        Assert.Equal(fileMock.Object.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture), resultContent.LastModifiedTime);
        Assert.False(resultContent.IsAnonymousUser);
        Assert.Equal("nameId", resultContent.UserId);
        Assert.Equal("testUser", resultContent.UserFriendlyName);
    }

    [Fact]
    public async Task CheckFileInfo_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        // Act
        var result = await controller.CheckFileInfo("file_id");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region GetFile

    [Fact]
    public async Task GetFile_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        // Act
        var result = await controller.GetFile("file_id");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetFile_FileExistsWithinMaxSize_ReturnsFileStreamResult()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 500);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        // Act
        var result = await controller.GetFile(fileId, maximumExpectedSize: 1000);

        // Assert
        Assert.IsType<FileStreamResult>(result);
    }

    [Fact]
    public async Task GetFile_FileExceedsMaxExpectedSize_ReturnsPreconditionFailed()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 2000);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        // Act
        var result = await controller.GetFile(fileId, maximumExpectedSize: 1000);

        // Assert
        Assert.IsType<PreconditionFailedResult>(result);
    }

    [Fact]
    public async Task GetFile_WithVersion_SetsVersionHeader()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 100, version: "v2");
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        // Act
        var result = await controller.GetFile(fileId);

        // Assert
        Assert.IsType<FileStreamResult>(result);
        Assert.Equal("v2", controller.Response.Headers[WopiHeaders.ITEM_VERSION].ToString());
    }

    #endregion

    #region GetEcosystem

    [Fact]
    public async Task GetEcosystem_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        // Act
        var result = await controller.GetEcosystem("file_id");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetEcosystem_Success_ReturnsUrlResponse()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        // Act
        var result = await controller.GetEcosystem(fileId);

        // Assert
        var jsonResult = Assert.IsType<JsonResult<UrlResponse>>(result);
        Assert.NotNull(jsonResult.Value);
    }

    #endregion

    #region EnumerateAncestors

    [Fact]
    public async Task EnumerateAncestors_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        // Act
        var result = await controller.EnumerateAncestors("file_id");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EnumerateAncestors_Success_ReturnsAncestors()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var folderMock = new Mock<IWopiFolder>();
        folderMock.SetupGet(f => f.Name).Returns("parentFolder");
        folderMock.SetupGet(f => f.Identifier).Returns("parentFolderId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([folderMock.Object]);

        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);

        // Act
        var result = await controller.EnumerateAncestors(fileId);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var response = Assert.IsType<EnumerateAncestorsResponse>(jsonResult.Value);
        Assert.Single(response.AncestorsWithRootFirst);
    }

    #endregion

    #region PutUserInfo

    [Fact]
    public async Task PutUserInfo_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        // Act
        var result = await controller.PutUserInfo("file_id", "user_info");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PutUserInfo_Success()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = new Mock<IWopiFile>();
        fileMock.SetupGet(f => f.Owner).Returns("ownerId");
        fileMock.SetupGet(f => f.Version).Returns("1.0");
        fileMock.SetupGet(f => f.Name).Returns("test");
        fileMock.SetupGet(f => f.Extension).Returns("txt");
        fileMock.SetupGet(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.SetupGet(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.GetReadStream(It.IsAny<CancellationToken>())).ReturnsAsync(new System.IO.MemoryStream());

        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fileMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "nameId"),
                        new Claim(ClaimTypes.Name, "testUser")
                    ], "TestAuthentication"))
            }
        };

        // Act
        var result = await controller.PutUserInfo(fileId, "custom user info");

        // Assert
        Assert.IsType<OkResult>(result);
        Assert.Equal("custom user info", memoryCache.Get("UserInfo-nameId"));
    }

    #endregion

    #region DeleteFile

    [Fact]
    public async Task DeleteFile_NoWritableStorageProvider()
    {
        // Arrange
        controller = new FilesController(
                    storageProviderMock.Object,
                    memoryCache);
        // Act
        var result = await controller.DeleteFile("file_id");
        // Assert
        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task DeleteFile_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);
        // Act
        var result = await controller.DeleteFile("file_id");
        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteFile_FileIsLocked_ReturnsConflict()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = new Mock<IWopiFile>();
        fileMock.SetupGet(f => f.Owner).Returns("ownerId");
        fileMock.SetupGet(f => f.Version).Returns("1.0");
        fileMock.SetupGet(f => f.Name).Returns("test");
        fileMock.SetupGet(f => f.Extension).Returns("txt");
        fileMock.SetupGet(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.SetupGet(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.GetReadStream(It.IsAny<CancellationToken>())).ReturnsAsync(new System.IO.MemoryStream());

        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fileMock.Object);
        var wopiLockInfo = new WopiLockInfo() { LockId = "lockId", FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out wopiLockInfo)).Returns(true);

        // Act
        var result = await controller.DeleteFile(fileId);

        // Assert
        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task DeleteFile_Success_ReturnsOk()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        writableStorageProviderMock
            .Setup(w => w.DeleteWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await controller.DeleteFile(fileId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task DeleteFile_DeleteFails_ReturnsInternalServerError()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        writableStorageProviderMock
            .Setup(w => w.DeleteWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await controller.DeleteFile(fileId);

        // Assert
        Assert.IsType<InternalServerErrorResult>(result);
    }

    #endregion

    #region RenameFile

    [Fact]
    public async Task RenameFile_NoWritableStorageProvider_ReturnsNotImplemented()
    {
        // Arrange
        controller = new FilesController(storageProviderMock.Object, memoryCache)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.RenameFile("file_id", new UtfString());

        // Assert
        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task RenameFile_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);

        // Act
        var result = await controller.RenameFile("file_id", UtfString.FromDecoded("newName"));

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RenameFile_FileIsLocked_WithDifferentLockId_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        var existingLock = new WopiLockInfo { LockId = "differentLockId", FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out existingLock)).Returns(true);

        // Act
        var result = await controller.RenameFile(fileId, UtfString.FromDecoded("newName"), lockIdentifier: "myLockId");

        // Assert
        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task RenameFile_InvalidName_ReturnsBadRequest()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await controller.RenameFile(fileId, UtfString.FromDecoded("invalid|name"));

        // Assert
        Assert.IsType<BadRequestResult>(result);
        Assert.Equal("Specified name is illegal", controller.Response.Headers[WopiHeaders.INVALID_FILE_NAME].ToString());
    }

    [Fact]
    public async Task RenameFile_Success_ReturnsJsonWithNewName()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("newName.txt");
        writableStorageProviderMock
            .Setup(w => w.RenameWopiResource<IWopiFile>(fileId, "newName.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await controller.RenameFile(fileId, UtfString.FromDecoded("newName"));

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonResult.Value);
    }

    [Fact]
    public async Task RenameFile_RenameFails_ReturnsInternalServerError()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("newName.txt");
        writableStorageProviderMock
            .Setup(w => w.RenameWopiResource<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await controller.RenameFile(fileId, UtfString.FromDecoded("newName"));

        // Assert
        Assert.IsType<InternalServerErrorResult>(result);
    }

    [Fact]
    public async Task RenameFile_ArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("invalid", "requestedName"));

        // Act
        var result = await controller.RenameFile(fileId, UtfString.FromDecoded("newName"));

        // Assert
        Assert.IsType<BadRequestResult>(result);
        Assert.Equal("Specified name is illegal", controller.Response.Headers[WopiHeaders.INVALID_FILE_NAME].ToString());
    }

    [Fact]
    public async Task RenameFile_FileNotFoundException_ReturnsNotFound()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        // Act
        var result = await controller.RenameFile(fileId, UtfString.FromDecoded("newName"));

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RenameFile_InvalidOperationException_ReturnsConflict()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        // Act
        var result = await controller.RenameFile(fileId, UtfString.FromDecoded("newName"));

        // Assert
        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task RenameFile_UnexpectedException_ReturnsInternalServerError()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("unexpected"));

        // Act
        var result = await controller.RenameFile(fileId, UtfString.FromDecoded("newName"));

        // Assert
        Assert.IsType<InternalServerErrorResult>(result);
    }

    #endregion

    #region PutFile

    [Fact]
    public async Task PutFile_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);

        // Act
        var result = await controller.PutFile("file_id");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PutFile_NoLock_EmptyFile_ReturnsOk()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 0);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope()
            }
        };

        // Act
        var result = await controller.PutFile(fileId, newLockIdentifier: null);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task PutFile_NoLock_NonEmptyFile_ReturnsConflict()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 1024);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        // Act
        var result = await controller.PutFile(fileId, newLockIdentifier: null);

        // Assert
        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task PutFile_WithLock_NoLockProvider_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        controller = new FilesController(storageProviderMock.Object, memoryCache)
        {
            Url = urlMock.Object,
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.PutFile(fileId, newLockIdentifier: "lock-id");

        // Assert
        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task PutFile_WithLock_Success_ReturnsOk()
    {
        // Arrange
        var fileId = "testFileId";
        var lockId = "lock-id";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);
        lockProviderMock
            .Setup(x => x.AddLock(fileId, lockId))
            .Returns(new WopiLockInfo { LockId = lockId, FileId = fileId });

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope()
            }
        };

        // Act
        var result = await controller.PutFile(fileId, newLockIdentifier: lockId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task PutFile_WithLock_ExistingDifferentLock_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        var existingLock = new WopiLockInfo { LockId = "other-lock", FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out existingLock)).Returns(true);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await controller.PutFile(fileId, newLockIdentifier: "my-lock");

        // Assert
        Assert.IsType<LockMismatchResult>(result);
    }

    #endregion

    #region PutRelativeFile

    [Fact]
    public async Task PutRelativeFile_NoWritableStorageProvider_ReturnsNotImplemented()
    {
        // Arrange
        controller = new FilesController(storageProviderMock.Object, memoryCache)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.PutRelativeFile("file_id");

        // Assert
        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);

        // Act
        var result = await controller.PutRelativeFile("file_id", relativeTarget: UtfString.FromDecoded("file.txt"));

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_BothHeadersPresent_ReturnsNotImplemented()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        // Act
        var result = await controller.PutRelativeFile(
            fileId,
            suggestedTarget: UtfString.FromDecoded("file.txt"),
            relativeTarget: UtfString.FromDecoded("file.txt"));

        // Assert
        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_BothHeadersMissing_ReturnsNotImplemented()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        // Act
        var result = await controller.PutRelativeFile(fileId);

        // Assert
        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_RelativeTarget_InvalidName_ReturnsBadRequest()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);

        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await controller.PutRelativeFile(fileId, relativeTarget: UtfString.FromDecoded("invalid|name.txt"));

        // Assert
        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_RelativeTarget_FileExists_OverwriteFalse_ReturnsConflict()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);
        var existingFile = CreateFileMock("existingFileId");

        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        storageProviderMock
            .Setup(s => s.GetWopiResourceByName<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile.Object);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("file_copy.txt");

        // Act
        var result = await controller.PutRelativeFile(
            fileId,
            relativeTarget: UtfString.FromDecoded("file.txt"),
            overwriteRelativeTarget: false);

        // Assert
        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_RelativeTarget_FileExists_OverwriteTrue_FileLocked_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);
        var existingFile = CreateFileMock("existingFileId");
        var lockInfo = new WopiLockInfo { LockId = "existingLock", FileId = "existingFileId" };

        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        storageProviderMock
            .Setup(s => s.GetWopiResourceByName<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile.Object);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        lockProviderMock
            .Setup(x => x.TryGetLock("existingFileId", out lockInfo))
            .Returns(true);

        // Act
        var result = await controller.PutRelativeFile(
            fileId,
            relativeTarget: UtfString.FromDecoded("file.txt"),
            overwriteRelativeTarget: true);

        // Assert
        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_RelativeTarget_NewFile_ReturnsJsonResult()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);
        var newFileMock = CreateFileMock("newFileId");

        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        storageProviderMock
            .Setup(s => s.GetWopiResourceByName<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        writableStorageProviderMock
            .Setup(w => w.CreateWopiChildResource<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFileMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope()
            }
        };

        // Act
        var result = await controller.PutRelativeFile(
            fileId,
            relativeTarget: UtfString.FromDecoded("newfile.txt"));

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<ChildFile>(jsonResult.Value);
    }

    [Fact]
    public async Task PutRelativeFile_SuggestedTarget_ExtensionOnly_ReturnsJsonResult()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);
        var newFileMock = CreateFileMock("newFileId");

        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test.docx");
        writableStorageProviderMock
            .Setup(w => w.CreateWopiChildResource<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFileMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope()
            }
        };

        // Act
        // suggestedTarget starting with "." → extension-only mode
        var result = await controller.PutRelativeFile(
            fileId,
            suggestedTarget: UtfString.FromDecoded(".docx"));

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<ChildFile>(jsonResult.Value);
    }

    [Fact]
    public async Task PutRelativeFile_SuggestedTarget_InvalidName_ReturnsBadRequest()
    {
        // Arrange
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);

        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await controller.PutRelativeFile(
            fileId,
            suggestedTarget: UtfString.FromDecoded("invalid|file.txt"));

        // Assert
        Assert.IsType<BadRequestResult>(result);
    }

    #endregion

    #region ProcessLock

    // Helper method to set up a simple file mock
    private void SetupFileMock(string fileId, string version = "1.0")
    {
        var fileMock = new Mock<IWopiFile>();
        fileMock.SetupGet(f => f.Version).Returns(version);

        storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
    }

    [Fact]
    public async Task ProcessLock_LockingNotSupported_ReturnsLockMismatchResult()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        controller = new FilesController(
            storageProviderMock.Object,
            memoryCache)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var wopiOverrideHeader = WopiFileOperations.Lock;

        // Act
        var result = await controller.ProcessLock(fileId, wopiOverrideHeader);

        // Assert
        var lockMismatchResult = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("Locking is not supported", lockMismatchResult.Reason);
    }

    [Fact]
    public async Task ProcessLock_GetLock_ReturnsOkResultWithLockHeader()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var wopiOverrideHeader = WopiFileOperations.GetLock;
        var lockInfo = new WopiLockInfo { LockId = "existing-lock-id", FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);

        // Act
        var result = await controller.ProcessLock(fileId, wopiOverrideHeader);

        // Assert
        Assert.IsType<OkResult>(result);
        Assert.Equal("existing-lock-id", controller.Response.Headers[WopiHeaders.LOCK]);
    }

    [Fact]
    public async Task ProcessLock_GetLock_NoLockInfo_ReturnsLockMismatchResult()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var wopiOverrideHeader = WopiFileOperations.GetLock;
        WopiLockInfo? lockInfo = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);

        // Act
        var result = await controller.ProcessLock(fileId, wopiOverrideHeader);

        // Assert
        var lockMismatchResult = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("Missing existing lock", lockMismatchResult.Reason);
    }

    [Fact]
    public async Task ProcessLock_GetLock_Expired_ReturnsOkResultWithLockHeader()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var wopiOverrideHeader = WopiFileOperations.GetLock;
        var lockInfo = new WopiLockInfo { LockId = "existing-lock-id", FileId = fileId, DateCreated = DateTimeOffset.MinValue };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(false);

        // Act
        var result = await controller.ProcessLock(fileId, wopiOverrideHeader);

        // Assert
        Assert.IsType<OkResult>(result);
        Assert.Equal(WopiHeaders.EMPTY_LOCK_VALUE, controller.Response.Headers[WopiHeaders.LOCK]);
    }

    [Fact]
    public async Task ProcessLock_LockWithoutOldLockIdentifier_ReturnsOkResult()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var wopiOverrideHeader = WopiFileOperations.Lock;
        var newLockIdentifier = "new-lock-id";
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out It.Ref<WopiLockInfo?>.IsAny)).Returns(false);
        lockProviderMock.Setup(x => x.AddLock(fileId, newLockIdentifier)).Returns(new WopiLockInfo { LockId = newLockIdentifier, FileId = fileId });

        // Act
        var result = await controller.ProcessLock(fileId, wopiOverrideHeader, newLockIdentifier: newLockIdentifier);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Unlock_ReturnsOkResult()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var wopiOverrideHeader = WopiFileOperations.Unlock;
        var newLockIdentifier = "existing-lock-id";
        var lockInfo = new WopiLockInfo { LockId = newLockIdentifier, FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);
        lockProviderMock.Setup(x => x.RemoveLock(fileId)).Returns(true);

        // Act
        var result = await controller.ProcessLock(fileId, wopiOverrideHeader, newLockIdentifier: newLockIdentifier);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessLock_RefreshLock_ReturnsOkResult()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var wopiOverrideHeader = WopiFileOperations.RefreshLock;
        var newLockIdentifier = "existing-lock-id";
        var lockInfo = new WopiLockInfo { LockId = newLockIdentifier, FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);
        lockProviderMock.Setup(x => x.RefreshLock(fileId, null)).Returns(true);

        // Act
        var result = await controller.ProcessLock(fileId, wopiOverrideHeader, newLockIdentifier: newLockIdentifier);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Lock_EmptyNewLockId_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);

        // Act
        var result = await controller.ProcessLock(fileId, WopiFileOperations.Lock, newLockIdentifier: null);

        // Assert
        var lockMismatch = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("Missing new lock identifier", lockMismatch.Reason);
    }

    [Fact]
    public async Task ProcessLock_Lock_AlreadyLocked_SameLockId_RefreshesLock()
    {
        // Arrange
        var fileId = "test-file-id";
        var lockId = "same-lock-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = lockId, FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);
        lockProviderMock.Setup(x => x.RefreshLock(fileId, It.IsAny<string?>())).Returns(true);

        // Act
        var result = await controller.ProcessLock(fileId, WopiFileOperations.Lock, newLockIdentifier: lockId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Lock_AlreadyLocked_DifferentLockId_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = "existing-lock", FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);

        // Act
        var result = await controller.ProcessLock(fileId, WopiFileOperations.Lock, newLockIdentifier: "different-lock");

        // Assert
        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Lock_AddLockFails_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);
        lockProviderMock.Setup(x => x.AddLock(fileId, It.IsAny<string>())).Returns((WopiLockInfo?)null);

        // Act
        var result = await controller.ProcessLock(fileId, WopiFileOperations.Lock, newLockIdentifier: "new-lock");

        // Assert
        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Put_MatchingOldLock_Success_ReturnsOk()
    {
        // Arrange
        var fileId = "test-file-id";
        var oldLockId = "old-lock-id";
        var newLockId = "new-lock-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = oldLockId, FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);
        lockProviderMock.Setup(x => x.RefreshLock(fileId, newLockId)).Returns(true);

        // Act
        var result = await controller.ProcessLock(
            fileId,
            WopiFileOperations.Put,
            oldLockIdentifier: oldLockId,
            newLockIdentifier: newLockId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Put_MismatchingOldLock_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = "actual-lock", FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);

        // Act
        var result = await controller.ProcessLock(
            fileId,
            WopiFileOperations.Put,
            oldLockIdentifier: "wrong-old-lock",
            newLockIdentifier: "new-lock");

        // Assert
        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Put_NotLocked_WithOldLock_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);

        // Act
        var result = await controller.ProcessLock(
            fileId,
            WopiFileOperations.Put,
            oldLockIdentifier: "old-lock",
            newLockIdentifier: "new-lock");

        // Assert
        var lockMismatch = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("File not locked", lockMismatch.Reason);
    }

    [Fact]
    public async Task ProcessLock_Unlock_LockIdMismatch_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = "actual-lock", FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);

        // Act
        var result = await controller.ProcessLock(
            fileId,
            WopiFileOperations.Unlock,
            newLockIdentifier: "wrong-lock");

        // Assert
        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Unlock_NotLocked_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);

        // Act
        var result = await controller.ProcessLock(
            fileId,
            WopiFileOperations.Unlock,
            newLockIdentifier: "some-lock");

        // Assert
        var lockMismatch = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("File not locked", lockMismatch.Reason);
    }

    [Fact]
    public async Task ProcessLock_Unlock_RemoveLockFails_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "test-file-id";
        var lockId = "lock-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = lockId, FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);
        lockProviderMock.Setup(x => x.RemoveLock(fileId)).Returns(false);

        // Act
        var result = await controller.ProcessLock(
            fileId,
            WopiFileOperations.Unlock,
            newLockIdentifier: lockId);

        // Assert
        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_RefreshLock_NotLocked_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        WopiLockInfo? noLock = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out noLock)).Returns(false);

        // Act
        var result = await controller.ProcessLock(
            fileId,
            WopiFileOperations.RefreshLock,
            newLockIdentifier: "some-lock");

        // Assert
        var lockMismatch = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("File not locked", lockMismatch.Reason);
    }

    [Fact]
    public async Task ProcessLock_RefreshLock_EmptyNewLockId_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "test-file-id";
        var lockId = "lock-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = lockId, FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);

        // Act
        var result = await controller.ProcessLock(
            fileId,
            WopiFileOperations.RefreshLock,
            newLockIdentifier: null);

        // Assert
        var lockMismatch = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("Missing new lock identifier", lockMismatch.Reason);
    }

    [Fact]
    public async Task ProcessLock_RefreshLock_LockIdMismatch_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = "actual-lock", FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);

        // Act
        var result = await controller.ProcessLock(
            fileId,
            WopiFileOperations.RefreshLock,
            newLockIdentifier: "different-lock");

        // Assert
        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_RefreshLock_RefreshFails_ReturnsLockMismatch()
    {
        // Arrange
        var fileId = "test-file-id";
        var lockId = "lock-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = lockId, FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);
        lockProviderMock.Setup(x => x.RefreshLock(fileId, It.IsAny<string?>())).Returns(false);

        // Act
        var result = await controller.ProcessLock(
            fileId,
            WopiFileOperations.RefreshLock,
            newLockIdentifier: lockId);

        // Assert
        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_UnknownOverride_ReturnsNotImplemented()
    {
        // Arrange
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        // Act
        var result = await controller.ProcessLock(fileId, "UNKNOWN_OVERRIDE");

        // Assert
        Assert.IsType<NotImplementedResult>(result);
    }

    #endregion
}
