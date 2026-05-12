using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
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
    private readonly Mock<IWopiStorageProvider> _storageProviderMock;
    private readonly Mock<IWopiWritableStorageProvider> _writableStorageProviderMock;
    private readonly Mock<IWopiPermissionProvider> _permissionProviderMock;
    private readonly Mock<IWopiAccessTokenService> _accessTokenServiceMock;
    private readonly Mock<IOptions<WopiHostOptions>> _wopiHostOptionsMock;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<IWopiLockProvider> _lockProviderMock;
    private readonly Mock<IUrlHelper> _urlMock;
    private FilesController _controller;

    public FilesControllerTests()
    {
        _storageProviderMock = new Mock<IWopiStorageProvider>();
        _writableStorageProviderMock = new Mock<IWopiWritableStorageProvider>();
        _permissionProviderMock = new Mock<IWopiPermissionProvider>();
        _permissionProviderMock
            .Setup(_ => _.GetFilePermissionsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IWopiFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiFilePermissions.UserCanWrite | WopiFilePermissions.UserCanRename | WopiFilePermissions.UserCanAttend | WopiFilePermissions.UserCanPresent);
        _wopiHostOptionsMock = new Mock<IOptions<WopiHostOptions>>();
        _wopiHostOptionsMock
            .SetupGet(o => o.Value)
            .Returns(new WopiHostOptions()
            {
                StorageProviderAssemblyName = "test",
                LockProviderAssemblyName = "test",
                OnCheckFileInfo = o => Task.FromResult(o.CheckFileInfo),
                ClientUrl = new Uri("http://localhost:5000"),
            });
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _lockProviderMock = new Mock<IWopiLockProvider>();
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

        _controller = new FilesController(
            _storageProviderMock.Object,
            _memoryCache,
            _writableStorageProviderMock.Object,
            _lockProviderMock.Object)
        {
            Url = _urlMock.Object,
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
        fileMock.SetupGet(f => f.Exists).Returns(size > 0);
        fileMock.SetupGet(f => f.Identifier).Returns(fileId);
        fileMock.Setup(f => f.OpenReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new System.IO.MemoryStream());
        fileMock.Setup(f => f.OpenWriteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new System.IO.MemoryStream());
        return fileMock;
    }

    #region CheckFileInfo

    [Fact]
    public async Task GetCheckFileInfo_FileNotFound_ReturnsNotFound()
    {
        var fileId = "testFileId";
        _storageProviderMock.Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.CheckFileInfo(fileId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetCheckFileInfo_Success_ReturnsFileInfoForAnonymous()
    {
        var fileId = "testFileId";
        var fileMock = new Mock<IWopiFile>();
        fileMock.SetupGet(f => f.Owner).Returns("ownerId");
        fileMock.SetupGet(f => f.Version).Returns("1.0");
        fileMock.SetupGet(f => f.Name).Returns("test");
        fileMock.SetupGet(f => f.Extension).Returns("txt");
        fileMock.SetupGet(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.SetupGet(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.OpenReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new System.IO.MemoryStream());
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fileMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope(_permissionProviderMock.Object),
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                    [
                        new(ClaimTypes.NameIdentifier, "userId"),
                        new(ClaimTypes.Name, "test")
                    ])),
            }
        };

        var result = await _controller.CheckFileInfo(fileId);

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
        var fileId = "testFileId";
        var fileMock = new Mock<IWopiFile>();
        fileMock.SetupGet(f => f.Owner).Returns("ownerId");
        fileMock.SetupGet(f => f.Version).Returns("1.0");
        fileMock.SetupGet(f => f.Name).Returns("test");
        fileMock.SetupGet(f => f.Extension).Returns("txt");
        fileMock.SetupGet(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.SetupGet(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.OpenReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new System.IO.MemoryStream());

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fileMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope(_permissionProviderMock.Object),
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "nameId"),
                        new Claim(ClaimTypes.Name, "testUser")
                    ], "TestAuthentication"))
            }
        };

        var result = await _controller.CheckFileInfo(fileId);

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
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.CheckFileInfo("file_id");

        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region GetFile

    [Fact]
    public async Task GetFile_FileNotFound_ReturnsNotFound()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.GetFile("file_id");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetFile_FileExistsWithinMaxSize_ReturnsFileStreamResult()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 500);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        var result = await _controller.GetFile(fileId, maximumExpectedSize: 1000);

        Assert.IsType<FileStreamResult>(result);
    }

    [Fact]
    public async Task GetFile_FileExceedsMaxExpectedSize_ReturnsPreconditionFailed()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 2000);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        var result = await _controller.GetFile(fileId, maximumExpectedSize: 1000);

        Assert.IsType<PreconditionFailedResult>(result);
    }

    [Fact]
    public async Task GetFile_WithVersion_SetsVersionHeader()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 100, version: "v2");
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        var result = await _controller.GetFile(fileId);

        Assert.IsType<FileStreamResult>(result);
        Assert.Equal("v2", _controller.Response.Headers[WopiHeaders.ITEM_VERSION].ToString());
    }

    #endregion

    #region GetEcosystem

    [Fact]
    public async Task GetEcosystem_FileNotFound_ReturnsNotFound()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.GetEcosystem("file_id", _accessTokenServiceMock.Object);

        Assert.IsType<NotFoundResult>(result);
        _accessTokenServiceMock.Verify(t => t.IssueAsync(It.IsAny<WopiAccessTokenRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetEcosystem_Success_ReturnsUrlResponse()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        SetAuthenticatedUser();

        var result = await _controller.GetEcosystem(fileId, _accessTokenServiceMock.Object);

        var jsonResult = Assert.IsType<JsonResult<UrlResponse>>(result);
        Assert.NotNull(jsonResult.Value);
    }

    [Fact]
    public async Task GetEcosystem_IssuesFreshMinimumPrivilegeToken_NotInbound()
    {
        // Token-trading prevention: the URL handed back to the WOPI client must carry a
        // FRESH access token, not the inbound one, and that token must grant the minimum
        // privileges required by the URL it lives in (CheckEcosystem -> None).
        // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/concepts#preventing-token-trading
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        SetAuthenticatedUser();

        UrlRouteContext? captured = null;
        _urlMock
            .Setup(_ => _.RouteUrl(It.IsAny<UrlRouteContext>()))
            .Callback<UrlRouteContext>(rc => captured = rc)
            .Returns("https://localhost/wopi/ecosystem");

        await _controller.GetEcosystem(fileId, _accessTokenServiceMock.Object);

        _accessTokenServiceMock.Verify(t => t.IssueAsync(
            It.Is<WopiAccessTokenRequest>(r =>
                r.UserId == "alice" &&
                r.ResourceId == fileId &&
                r.ResourceType == WopiResourceType.File &&
                r.FilePermissions == WopiFilePermissions.None),
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(captured);
        var values = new RouteValueDictionary(captured!.Values);
        Assert.Equal("FRESH-TOKEN", values["access_token"]);
    }

    private void SetAuthenticatedUser()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope(_permissionProviderMock.Object),
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "alice"),
                    new Claim(ClaimTypes.Name, "Alice Example"),
                    new Claim(ClaimTypes.Email, "alice@example.com"),
                ], "Test")),
            },
        };
    }

    #endregion

    #region EnumerateAncestors

    [Fact]
    public async Task EnumerateAncestors_FileNotFound_ReturnsNotFound()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.EnumerateAncestors("file_id");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EnumerateAncestors_Success_ReturnsAncestors()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var folderMock = new Mock<IWopiFolder>();
        folderMock.SetupGet(f => f.Name).Returns("parentFolder");
        folderMock.SetupGet(f => f.Identifier).Returns("parentFolderId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([folderMock.Object]);

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);

        var result = await _controller.EnumerateAncestors(fileId);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var response = Assert.IsType<EnumerateAncestorsResponse>(jsonResult.Value);
        Assert.Single(response.AncestorsWithRootFirst);
    }

    #endregion

    #region PutUserInfo

    [Fact]
    public async Task PutUserInfo_FileNotFound_ReturnsNotFound()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);

        var result = await _controller.PutUserInfo("file_id", "user_info");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PutUserInfo_Success()
    {
        var fileId = "testFileId";
        var fileMock = new Mock<IWopiFile>();
        fileMock.SetupGet(f => f.Owner).Returns("ownerId");
        fileMock.SetupGet(f => f.Version).Returns("1.0");
        fileMock.SetupGet(f => f.Name).Returns("test");
        fileMock.SetupGet(f => f.Extension).Returns("txt");
        fileMock.SetupGet(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.SetupGet(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.OpenReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new System.IO.MemoryStream());

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fileMock.Object);

        _controller.ControllerContext = new ControllerContext
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

        var result = await _controller.PutUserInfo(fileId, "custom user info");

        Assert.IsType<OkResult>(result);
        Assert.Equal("custom user info", _memoryCache.Get("UserInfo-nameId"));
    }

    #endregion

    #region DeleteFile

    [Fact]
    public async Task DeleteFile_FileNotFound_ReturnsNotFound()
    {
        _storageProviderMock.Setup(sp => sp.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);
        var result = await _controller.DeleteFile("file_id");
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteFile_FileIsLocked_ReturnsConflict()
    {
        var fileId = "testFileId";
        var fileMock = new Mock<IWopiFile>();
        fileMock.SetupGet(f => f.Owner).Returns("ownerId");
        fileMock.SetupGet(f => f.Version).Returns("1.0");
        fileMock.SetupGet(f => f.Name).Returns("test");
        fileMock.SetupGet(f => f.Extension).Returns("txt");
        fileMock.SetupGet(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        fileMock.SetupGet(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.OpenReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new System.IO.MemoryStream());

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fileMock.Object);
        var wopiLockInfo = new WopiLockInfo() { LockId = "lockId", FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(wopiLockInfo);

        var result = await _controller.DeleteFile(fileId);

        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task DeleteFile_Success_ReturnsOk()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock
            .Setup(w => w.DeleteWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DeleteFile(fileId);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task DeleteFile_DeleteFails_ReturnsInternalServerError()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock
            .Setup(w => w.DeleteWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.DeleteFile(fileId);

        Assert.IsType<InternalServerErrorResult>(result);
    }

    #endregion

    #region RenameFile

    [Fact]
    public async Task RenameFile_FileNotFound_ReturnsNotFound()
    {
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);

        var result = await _controller.RenameFile("file_id", UtfString.FromDecoded("newName"));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RenameFile_FileIsLocked_WithDifferentLockId_ReturnsLockMismatch()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        var existingLock = new WopiLockInfo { LockId = "differentLockId", FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(existingLock);

        var result = await _controller.RenameFile(fileId, UtfString.FromDecoded("newName"), lockIdentifier: "myLockId");

        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task RenameFile_InvalidName_ReturnsBadRequest()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.RenameFile(fileId, UtfString.FromDecoded("invalid|name"));

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal("Specified name is illegal", _controller.Response.Headers[WopiHeaders.INVALID_FILE_NAME].ToString());
    }

    [Fact]
    public async Task RenameFile_Success_ReturnsJsonWithNewName()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("newName.txt");
        _writableStorageProviderMock
            .Setup(w => w.RenameWopiResource<IWopiFile>(fileId, "newName.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.RenameFile(fileId, UtfString.FromDecoded("newName"));

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonResult.Value);
    }

    [Fact]
    public async Task RenameFile_RenameFails_ReturnsInternalServerError()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("newName.txt");
        _writableStorageProviderMock
            .Setup(w => w.RenameWopiResource<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.RenameFile(fileId, UtfString.FromDecoded("newName"));

        Assert.IsType<InternalServerErrorResult>(result);
    }

    [Fact]
    public async Task RenameFile_ArgumentException_ReturnsBadRequest()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("invalid", "requestedName"));

        var result = await _controller.RenameFile(fileId, UtfString.FromDecoded("newName"));

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal("Specified name is illegal", _controller.Response.Headers[WopiHeaders.INVALID_FILE_NAME].ToString());
    }

    [Fact]
    public async Task RenameFile_FileNotFoundException_ReturnsNotFound()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        var result = await _controller.RenameFile(fileId, UtfString.FromDecoded("newName"));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RenameFile_InvalidOperationException_ReturnsConflict()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        var result = await _controller.RenameFile(fileId, UtfString.FromDecoded("newName"));

        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task RenameFile_UnexpectedException_ReturnsInternalServerError()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFolder>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("unexpected"));

        var result = await _controller.RenameFile(fileId, UtfString.FromDecoded("newName"));

        Assert.IsType<InternalServerErrorResult>(result);
    }

    #endregion

    #region PutFile

    [Fact]
    public async Task PutFile_FileNotFound_ReturnsNotFound()
    {
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);

        var result = await _controller.PutFile("file_id");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PutFile_NoLock_EmptyFile_ReturnsOk()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 0);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope()
            }
        };

        var result = await _controller.PutFile(fileId, newLockIdentifier: null);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task PutFile_NoLock_NonEmptyFile_ReturnsLockMismatchWithEmptyLockHeader()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 1024);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        var result = await _controller.PutFile(fileId, newLockIdentifier: null);

        // Spec (PutFile): non-empty unlocked file must respond 409 with X-WOPI-Lock set to the empty string.
        Assert.IsType<LockMismatchResult>(result);
        Assert.True(_controller.Response.Headers.ContainsKey(WopiHeaders.LOCK));
        Assert.Equal(string.Empty, _controller.Response.Headers[WopiHeaders.LOCK].ToString());
    }

    [Fact]
    public async Task PutFile_WithLock_NoLockProvider_ReturnsLockMismatch()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        _controller = new FilesController(_storageProviderMock.Object, _memoryCache)
        {
            Url = _urlMock.Object,
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await _controller.PutFile(fileId, newLockIdentifier: "lock-id");

        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task PutFile_WithLock_Success_ReturnsOk()
    {
        var fileId = "testFileId";
        var lockId = "lock-id";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _lockProviderMock
            .Setup(x => x.AddLockAsync(fileId, lockId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiLockInfo { LockId = lockId, FileId = fileId });

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope()
            }
        };

        var result = await _controller.PutFile(fileId, newLockIdentifier: lockId);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task PutFile_WithEditorsHeader_InvokesOnPutFileWithParsedContributors()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 0);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        WopiPutFileContext? captured = null;
        var optionsWithCallback = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            OnPutFile = ctx => { captured = ctx; return Task.CompletedTask; },
        });

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope<IOptions<WopiHostOptions>>(optionsWithCallback),
            }
        };

        var result = await _controller.PutFile(fileId, newLockIdentifier: null, editors: " alice , bob ,, charlie");

        Assert.IsType<OkResult>(result);
        Assert.NotNull(captured);
        Assert.Equal(fileMock.Object, captured.File);
        // Spec: comma-delimited list of UserIds. Empty entries from extra commas are dropped;
        // surrounding whitespace is trimmed.
        Assert.Equal(["alice", "bob", "charlie"], captured.Editors);
    }

    [Fact]
    public async Task PutFile_NoEditorsHeader_InvokesOnPutFileWithEmptyContributorList()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 0);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        WopiPutFileContext? captured = null;
        var optionsWithCallback = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            OnPutFile = ctx => { captured = ctx; return Task.CompletedTask; },
        });

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope<IOptions<WopiHostOptions>>(optionsWithCallback),
            }
        };

        var result = await _controller.PutFile(fileId, newLockIdentifier: null, editors: null);

        Assert.IsType<OkResult>(result);
        Assert.NotNull(captured);
        Assert.Empty(captured.Editors);
    }

    [Fact]
    public async Task PutFile_RequestExceedsMaxFileSize_Returns413()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 0);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        var optionsWithLimit = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            MaxFileSize = 1024,
        });

        var httpContext = new DefaultHttpContext
        {
            ServiceScopeFactory = TestUtils.CreateServiceScope<IOptions<WopiHostOptions>>(optionsWithLimit),
        };
        httpContext.Request.ContentLength = 4096;

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _controller.PutFile(fileId, newLockIdentifier: null);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, status.StatusCode);
    }

    [Fact]
    public async Task PutFile_WithLock_ExistingDifferentLock_ReturnsLockMismatch()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        var existingLock = new WopiLockInfo { LockId = "other-lock", FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(existingLock);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await _controller.PutFile(fileId, newLockIdentifier: "my-lock");

        Assert.IsType<LockMismatchResult>(result);
    }

    #endregion

    #region PutRelativeFile

    [Fact]
    public async Task PutRelativeFile_FileNotFound_ReturnsNotFound()
    {
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);

        var result = await _controller.PutRelativeFile("file_id", relativeTarget: UtfString.FromDecoded("file.txt"));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_BothHeadersPresent_ReturnsNotImplemented()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        var result = await _controller.PutRelativeFile(
            fileId,
            suggestedTarget: UtfString.FromDecoded("file.txt"),
            relativeTarget: UtfString.FromDecoded("file.txt"));

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_BothHeadersMissing_ReturnsNotImplemented()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        var result = await _controller.PutRelativeFile(fileId);

        Assert.IsType<NotImplementedResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_RelativeTarget_InvalidName_ReturnsBadRequest()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.PutRelativeFile(fileId, relativeTarget: UtfString.FromDecoded("invalid|name.txt"));

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_RelativeTarget_FileExists_OverwriteFalse_ReturnsConflict()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);
        var existingFile = CreateFileMock("existingFileId");

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        _storageProviderMock
            .Setup(s => s.GetWopiResourceByName<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile.Object);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("file_copy.txt");

        var result = await _controller.PutRelativeFile(
            fileId,
            relativeTarget: UtfString.FromDecoded("file.txt"),
            overwriteRelativeTarget: false);

        Assert.IsType<ConflictResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_RelativeTarget_FileExists_OverwriteTrue_FileLocked_ReturnsLockMismatch()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);
        var existingFile = CreateFileMock("existingFileId");
        var lockInfo = new WopiLockInfo { LockId = "existingLock", FileId = "existingFileId" };

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        _storageProviderMock
            .Setup(s => s.GetWopiResourceByName<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile.Object);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _lockProviderMock
            .Setup(x => x.GetLockAsync("existingFileId", It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockInfo);

        var result = await _controller.PutRelativeFile(
            fileId,
            relativeTarget: UtfString.FromDecoded("file.txt"),
            overwriteRelativeTarget: true);

        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task PutRelativeFile_RelativeTarget_NewFile_ReturnsJsonResult()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);
        var newFileMock = CreateFileMock("newFileId");

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        _storageProviderMock
            .Setup(s => s.GetWopiResourceByName<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock
            .Setup(w => w.CreateWopiChildResource<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFileMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope()
            }
        };

        var result = await _controller.PutRelativeFile(
            fileId,
            relativeTarget: UtfString.FromDecoded("newfile.txt"));

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<ChildFile>(jsonResult.Value);
    }

    [Fact]
    public async Task PutRelativeFile_DeclaredSizeExceedsMaxFileSize_Returns413()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        var optionsWithLimit = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            MaxFileSize = 1024,
        });

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope<IOptions<WopiHostOptions>>(optionsWithLimit),
            }
        };

        // X-WOPI-Size declares 4096, MaxFileSize is 1024 → 413 short-circuits before write.
        var result = await _controller.PutRelativeFile(
            fileId,
            relativeTarget: UtfString.FromDecoded("newfile.txt"),
            declaredSize: 4096);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, status.StatusCode);
    }

    [Fact]
    public async Task PutRelativeFile_FileConversionAndSizeHeaders_SurfaceViaOnPutRelativeFile()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);
        var newFileMock = CreateFileMock("newFileId");

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        _storageProviderMock
            .Setup(s => s.GetWopiResourceByName<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock
            .Setup(w => w.CreateWopiChildResource<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFileMock.Object);

        WopiPutRelativeFileContext? captured = null;
        var optionsWithCallback = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            OnPutRelativeFile = ctx => { captured = ctx; return Task.CompletedTask; },
        });

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope<IOptions<WopiHostOptions>>(optionsWithCallback),
            }
        };

        var result = await _controller.PutRelativeFile(
            fileId,
            relativeTarget: UtfString.FromDecoded("newfile.txt"),
            fileConversion: "true",
            declaredSize: 4096);

        Assert.IsType<JsonResult>(result);
        Assert.NotNull(captured);
        Assert.Same(fileMock.Object, captured.OriginalFile);
        Assert.Same(newFileMock.Object, captured.NewFile);
        Assert.True(captured.IsFileConversion);
        Assert.Equal(4096, captured.DeclaredSize);
    }

    [Fact]
    public async Task PutRelativeFile_NoConversionOrSize_SurfaceFalseAndNullDefaults()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);
        var newFileMock = CreateFileMock("newFileId");

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        _storageProviderMock
            .Setup(s => s.GetWopiResourceByName<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _writableStorageProviderMock
            .Setup(w => w.CreateWopiChildResource<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFileMock.Object);

        WopiPutRelativeFileContext? captured = null;
        var optionsWithCallback = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            OnPutRelativeFile = ctx => { captured = ctx; return Task.CompletedTask; },
        });

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope<IOptions<WopiHostOptions>>(optionsWithCallback),
            }
        };

        var result = await _controller.PutRelativeFile(
            fileId,
            relativeTarget: UtfString.FromDecoded("newfile.txt"));

        Assert.IsType<JsonResult>(result);
        Assert.NotNull(captured);
        Assert.False(captured.IsFileConversion);
        Assert.Null(captured.DeclaredSize);
    }

    [Fact]
    public async Task PutRelativeFile_SuggestedTarget_ExtensionOnly_ReturnsJsonResult()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);
        var newFileMock = CreateFileMock("newFileId");

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        _writableStorageProviderMock
            .Setup(w => w.GetSuggestedName<IWopiFile>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test.docx");
        _writableStorageProviderMock
            .Setup(w => w.CreateWopiChildResource<IWopiFile>("parentId", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newFileMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope()
            }
        };

        // suggestedTarget starting with "." → extension-only mode
        var result = await _controller.PutRelativeFile(
            fileId,
            suggestedTarget: UtfString.FromDecoded(".docx"));

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.IsType<ChildFile>(jsonResult.Value);
    }

    [Fact]
    public async Task PutRelativeFile_SuggestedTarget_InvalidName_ReturnsBadRequest()
    {
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId);
        var parentFolder = new Mock<IWopiFolder>();
        parentFolder.SetupGet(f => f.Identifier).Returns("parentId");
        var ancestors = new ReadOnlyCollection<IWopiFolder>([parentFolder.Object]);

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
        _storageProviderMock
            .Setup(s => s.GetAncestors<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestors);
        _writableStorageProviderMock
            .Setup(w => w.CheckValidName<IWopiFile>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.PutRelativeFile(
            fileId,
            suggestedTarget: UtfString.FromDecoded("invalid|file.txt"));

        Assert.IsType<BadRequestResult>(result);
    }

    #endregion

    #region ProcessLock

    // Helper method to set up a simple file mock
    private void SetupFileMock(string fileId, string version = "1.0")
    {
        var fileMock = new Mock<IWopiFile>();
        fileMock.SetupGet(f => f.Version).Returns(version);

        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);
    }

    [Fact]
    public async Task ProcessLock_LockingNotSupported_ReturnsLockMismatchResult()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        _controller = new FilesController(
            _storageProviderMock.Object,
            _memoryCache)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var wopiOverrideHeader = WopiFileOperations.Lock;

        var result = await _controller.ProcessLock(fileId, wopiOverrideHeader);

        var lockMismatchResult = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("Locking is not supported", lockMismatchResult.Reason);
    }

    [Fact]
    public async Task ProcessLock_NewLockIdLongerThanMax_ReturnsBadRequestWithReasonHeader()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var oversized = new string('a', WopiLockInfo.MaxLockIdLength + 1);

        var result = await _controller.ProcessLock(
            fileId,
            wopiOverrideHeader: WopiFileOperations.Lock,
            newLockIdentifier: oversized);

        Assert.IsType<BadRequestResult>(result);
        Assert.True(_controller.Response.Headers.ContainsKey(WopiHeaders.LOCK_FAILURE_REASON));
        Assert.Contains(WopiLockInfo.MaxLockIdLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _controller.Response.Headers[WopiHeaders.LOCK_FAILURE_REASON].ToString());
    }

    [Fact]
    public async Task ProcessLock_OldLockIdLongerThanMax_ReturnsBadRequest()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var oversized = new string('z', WopiLockInfo.MaxLockIdLength + 1);

        var result = await _controller.ProcessLock(
            fileId,
            wopiOverrideHeader: WopiFileOperations.Lock,
            oldLockIdentifier: oversized,
            newLockIdentifier: "valid-new");

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task ProcessLock_LockIdAtMaxLength_IsAccepted()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        // exactly at the cap - must NOT be rejected.
        var atCap = new string('x', WopiLockInfo.MaxLockIdLength);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _lockProviderMock.Setup(x => x.AddLockAsync(fileId, atCap, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiLockInfo { LockId = atCap, FileId = fileId });

        var result = await _controller.ProcessLock(
            fileId,
            wopiOverrideHeader: WopiFileOperations.Lock,
            newLockIdentifier: atCap);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessLock_GetLock_ReturnsOkResultWithLockHeader()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var wopiOverrideHeader = WopiFileOperations.GetLock;
        var lockInfo = new WopiLockInfo { LockId = "existing-lock-id", FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);

        var result = await _controller.ProcessLock(fileId, wopiOverrideHeader);

        Assert.IsType<OkResult>(result);
        Assert.Equal("existing-lock-id", _controller.Response.Headers[WopiHeaders.LOCK]);
    }

    [Fact]
    public async Task ProcessLock_GetLock_NoExistingLock_ReturnsOkWithEmptyLockHeader()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var wopiOverrideHeader = WopiFileOperations.GetLock;
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);

        var result = await _controller.ProcessLock(fileId, wopiOverrideHeader);

        Assert.IsType<OkResult>(result);
        Assert.Equal(WopiHeaders.EMPTY_LOCK_VALUE, _controller.Response.Headers[WopiHeaders.LOCK]);
        // Spec (GetLock): "the host must return a 200 OK and include an X-WOPI-Lock response header
        // set to the empty string." Pinning the literal value guards against a regression to the
        // pre-#359 IIS-workaround behavior where this constant was a single space.
        Assert.Empty(WopiHeaders.EMPTY_LOCK_VALUE);
    }

    [Fact]
    public async Task ProcessLock_GetLock_NoExistingLock_HonorsConfiguredEmptyLockValue()
    {
        // Hosts running under IIS in-process opt back into the single-space workaround for
        // empty X-WOPI-Lock values via WopiHostOptions.EmptyLockHeaderValue. Verify the option
        // flows through HandleGetLock at request time.
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);

        var customOptions = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            EmptyLockHeaderValue = " ",
        });
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope<IOptions<WopiHostOptions>>(customOptions),
            }
        };

        var result = await _controller.ProcessLock(fileId, WopiFileOperations.GetLock);

        Assert.IsType<OkResult>(result);
        Assert.Equal(" ", _controller.Response.Headers[WopiHeaders.LOCK]);
    }

    [Fact]
    public async Task PutFile_NoLock_NonEmptyFile_HonorsConfiguredEmptyLockValue()
    {
        // Same option flow on the PutFile 409-with-empty-lock path (via LockMismatchResult).
        var fileId = "testFileId";
        var fileMock = CreateFileMock(fileId, size: 1024);
        _storageProviderMock
            .Setup(s => s.GetWopiResource<IWopiFile>(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileMock.Object);

        var customOptions = Options.Create(new WopiHostOptions
        {
            StorageProviderAssemblyName = "test",
            ClientUrl = new Uri("http://localhost:5000"),
            EmptyLockHeaderValue = " ",
        });
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                ServiceScopeFactory = TestUtils.CreateServiceScope<IOptions<WopiHostOptions>>(customOptions),
            }
        };

        var result = await _controller.PutFile(fileId, newLockIdentifier: null);

        Assert.IsType<LockMismatchResult>(result);
        Assert.Equal(" ", _controller.Response.Headers[WopiHeaders.LOCK].ToString());
    }

    [Fact]
    public async Task ProcessLock_LockWithoutOldLockIdentifier_ReturnsOkResult()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var wopiOverrideHeader = WopiFileOperations.Lock;
        var newLockIdentifier = "new-lock-id";
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _lockProviderMock.Setup(x => x.AddLockAsync(fileId, newLockIdentifier, It.IsAny<CancellationToken>())).ReturnsAsync(new WopiLockInfo { LockId = newLockIdentifier, FileId = fileId });

        var result = await _controller.ProcessLock(fileId, wopiOverrideHeader, newLockIdentifier: newLockIdentifier);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Unlock_ReturnsOkResult()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var wopiOverrideHeader = WopiFileOperations.Unlock;
        var newLockIdentifier = "existing-lock-id";
        var lockInfo = new WopiLockInfo { LockId = newLockIdentifier, FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);
        _lockProviderMock.Setup(x => x.RemoveLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.ProcessLock(fileId, wopiOverrideHeader, newLockIdentifier: newLockIdentifier);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessLock_RefreshLock_ReturnsOkResult()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var wopiOverrideHeader = WopiFileOperations.RefreshLock;
        var newLockIdentifier = "existing-lock-id";
        var lockInfo = new WopiLockInfo { LockId = newLockIdentifier, FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);
        _lockProviderMock.Setup(x => x.RefreshLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.ProcessLock(fileId, wopiOverrideHeader, newLockIdentifier: newLockIdentifier);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Lock_EmptyNewLockId_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);

        var result = await _controller.ProcessLock(fileId, WopiFileOperations.Lock, newLockIdentifier: null);

        var lockMismatch = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("Missing new lock identifier", lockMismatch.Reason);
    }

    [Fact]
    public async Task ProcessLock_Lock_AlreadyLocked_SameLockId_RefreshesLock()
    {
        var fileId = "test-file-id";
        var lockId = "same-lock-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = lockId, FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);
        _lockProviderMock.Setup(x => x.RefreshLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.ProcessLock(fileId, WopiFileOperations.Lock, newLockIdentifier: lockId);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Lock_AlreadyLocked_DifferentLockId_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = "existing-lock", FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);

        var result = await _controller.ProcessLock(fileId, WopiFileOperations.Lock, newLockIdentifier: "different-lock");

        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Lock_AddLockFails_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);
        _lockProviderMock.Setup(x => x.AddLockAsync(fileId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);

        var result = await _controller.ProcessLock(fileId, WopiFileOperations.Lock, newLockIdentifier: "new-lock");

        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Put_MatchingOldLock_EmptyNewLockId_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        var oldLockId = "old-lock-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = oldLockId, FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);

        var result = await _controller.ProcessLock(
            fileId,
            WopiFileOperations.Put,
            oldLockIdentifier: oldLockId,
            newLockIdentifier: null);

        var lockMismatch = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("Missing new lock identifier", lockMismatch.Reason);
    }

    [Fact]
    public async Task ProcessLock_Put_MatchingOldLock_Success_ReturnsOk()
    {
        var fileId = "test-file-id";
        var oldLockId = "old-lock-id";
        var newLockId = "new-lock-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = oldLockId, FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);
        _lockProviderMock
            .Setup(x => x.TryUnlockAndRelockAsync(fileId, newLockId, oldLockId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.ProcessLock(
            fileId,
            WopiFileOperations.Put,
            oldLockIdentifier: oldLockId,
            newLockIdentifier: newLockId);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Put_MismatchingOldLock_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = "actual-lock", FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);

        var result = await _controller.ProcessLock(
            fileId,
            WopiFileOperations.Put,
            oldLockIdentifier: "wrong-old-lock",
            newLockIdentifier: "new-lock");

        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Put_NotLocked_WithOldLock_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);

        var result = await _controller.ProcessLock(
            fileId,
            WopiFileOperations.Put,
            oldLockIdentifier: "old-lock",
            newLockIdentifier: "new-lock");

        var lockMismatch = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("File not locked", lockMismatch.Reason);
    }

    [Fact]
    public async Task ProcessLock_Unlock_LockIdMismatch_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = "actual-lock", FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);

        var result = await _controller.ProcessLock(
            fileId,
            WopiFileOperations.Unlock,
            newLockIdentifier: "wrong-lock");

        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_Unlock_NotLocked_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);

        var result = await _controller.ProcessLock(
            fileId,
            WopiFileOperations.Unlock,
            newLockIdentifier: "some-lock");

        var lockMismatch = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("File not locked", lockMismatch.Reason);
    }

    [Fact]
    public async Task ProcessLock_Unlock_RemoveLockFails_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        var lockId = "lock-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = lockId, FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);
        _lockProviderMock.Setup(x => x.RemoveLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.ProcessLock(
            fileId,
            WopiFileOperations.Unlock,
            newLockIdentifier: lockId);

        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_RefreshLock_NotLocked_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync((WopiLockInfo?)null);

        var result = await _controller.ProcessLock(
            fileId,
            WopiFileOperations.RefreshLock,
            newLockIdentifier: "some-lock");

        var lockMismatch = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("File not locked", lockMismatch.Reason);
    }

    [Fact]
    public async Task ProcessLock_RefreshLock_EmptyNewLockId_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        var lockId = "lock-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = lockId, FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);

        var result = await _controller.ProcessLock(
            fileId,
            WopiFileOperations.RefreshLock,
            newLockIdentifier: null);

        var lockMismatch = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("Missing new lock identifier", lockMismatch.Reason);
    }

    [Fact]
    public async Task ProcessLock_RefreshLock_LockIdMismatch_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = "actual-lock", FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);

        var result = await _controller.ProcessLock(
            fileId,
            WopiFileOperations.RefreshLock,
            newLockIdentifier: "different-lock");

        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_RefreshLock_RefreshFails_ReturnsLockMismatch()
    {
        var fileId = "test-file-id";
        var lockId = "lock-id";
        SetupFileMock(fileId);
        var lockInfo = new WopiLockInfo { LockId = lockId, FileId = fileId };
        _lockProviderMock.Setup(x => x.GetLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(lockInfo);
        _lockProviderMock.Setup(x => x.RefreshLockAsync(fileId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.ProcessLock(
            fileId,
            WopiFileOperations.RefreshLock,
            newLockIdentifier: lockId);

        Assert.IsType<LockMismatchResult>(result);
    }

    [Fact]
    public async Task ProcessLock_UnknownOverride_ReturnsNotImplemented()
    {
        var fileId = "test-file-id";
        SetupFileMock(fileId);

        var result = await _controller.ProcessLock(fileId, "UNKNOWN_OVERRIDE");

        Assert.IsType<NotImplementedResult>(result);
    }

    #endregion
}
