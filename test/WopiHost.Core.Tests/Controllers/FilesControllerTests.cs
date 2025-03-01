using System.Globalization;
using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Controllers;
using WopiHost.Core.Models;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Controllers;

public class FilesControllerTests
{
    private readonly Mock<IWopiStorageProvider> storageProviderMock;
    private readonly Mock<IWopiSecurityHandler> securityHandlerMock;
    private readonly Mock<IOptions<WopiHostOptions>> wopiHostOptionsMock;
    private readonly Mock<IAuthorizationService> authorizationServiceMock;
    private readonly Mock<IWopiLockProvider> lockProviderMock;
    private FilesController controller;

    public FilesControllerTests()
    {
        storageProviderMock = new Mock<IWopiStorageProvider>();
        securityHandlerMock = new Mock<IWopiSecurityHandler>();
        wopiHostOptionsMock = new Mock<IOptions<WopiHostOptions>>();
        authorizationServiceMock = new Mock<IAuthorizationService>();
        lockProviderMock = new Mock<IWopiLockProvider>();

        controller = new FilesController(
            storageProviderMock.Object,
            securityHandlerMock.Object,
            wopiHostOptionsMock.Object,
            authorizationServiceMock.Object,
            lockProviderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task GetCheckFileInfo_Unauthorized_ReturnsUnauthorized()
    {
        // Arrange
        var fileId = "testFileId";
        authorizationServiceMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());

        // Act
        var result = await controller.GetCheckFileInfo(fileId);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetCheckFileInfo_FileNotFound_ReturnsNotFound()
    {
        // Arrange
        var fileId = "testFileId";
        authorizationServiceMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        storageProviderMock.Setup(s => s.GetWopiFile(fileId)).Returns<IWopiFile>(null!);

        // Act
        var result = await controller.GetCheckFileInfo(fileId);

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

        authorizationServiceMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        storageProviderMock
            .Setup(s => s.GetWopiFile(fileId))
            .Returns(fileMock.Object);
        storageProviderMock
            .Setup(s => s.GetWopiCheckFileInfo(It.IsAny<IWopiFile>(), It.IsAny<WopiHostCapabilities>(), It.IsAny<ClaimsPrincipal?>(), It.IsAny<WopiCheckFileInfo>()))
            .Returns((WopiCheckFileInfo?)null);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await controller.GetCheckFileInfo(fileId);

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

        authorizationServiceMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        storageProviderMock
            .Setup(s => s.GetWopiFile(fileId))
            .Returns(fileMock.Object);
        storageProviderMock
            .Setup(s => s.GetWopiCheckFileInfo(It.IsAny<IWopiFile>(), It.IsAny<WopiHostCapabilities>(), It.IsAny<ClaimsPrincipal?>(), It.IsAny<WopiCheckFileInfo>()))
            .Returns((WopiCheckFileInfo?)null);

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
        var result = await controller.GetCheckFileInfo(fileId);

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
    public void ProcessLock_LockingNotSupported_ReturnsLockMismatchResult()
    {
        // Arrange
        controller = new FilesController(
            storageProviderMock.Object,
            securityHandlerMock.Object,
            wopiHostOptionsMock.Object,
            authorizationServiceMock.Object,
            null,
            null)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var fileId = "test-file-id";
        var wopiOverrideHeader = WopiFileOperations.Lock;

        // Act
        var result = controller.ProcessLock(fileId, wopiOverrideHeader);

        // Assert
        var lockMismatchResult = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("Locking is not supported", lockMismatchResult.Reason);
    }

    [Fact]
    public void ProcessLock_GetLock_ReturnsOkResultWithLockHeader()
    {
        // Arrange
        var fileId = "test-file-id";
        var wopiOverrideHeader = WopiFileOperations.GetLock;
        var lockInfo = new WopiLockInfo { LockId = "existing-lock-id", FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);

        // Act
        var result = controller.ProcessLock(fileId, wopiOverrideHeader);

        // Assert
        Assert.IsType<OkResult>(result);
        Assert.Equal("existing-lock-id", controller.Response.Headers[WopiHeaders.LOCK]);
    }

    [Fact]
    public void ProcessLock_GetLock_NoLockInfo_ReturnsLockMismatchResult()
    {
        // Arrange
        var fileId = "test-file-id";
        var wopiOverrideHeader = WopiFileOperations.GetLock;
        WopiLockInfo? lockInfo = null;
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);

        // Act
        var result = controller.ProcessLock(fileId, wopiOverrideHeader);

        // Assert
        var lockMismatchResult = Assert.IsType<LockMismatchResult>(result);
        Assert.Equal("Missing existing lock", lockMismatchResult.Reason);
    }

    [Fact]
    public void ProcessLock_GetLock_Expired_ReturnsOkResultWithLockHeader()
    {
        // Arrange
        var fileId = "test-file-id";
        var wopiOverrideHeader = WopiFileOperations.GetLock;
        var lockInfo = new WopiLockInfo { LockId = "existing-lock-id", FileId = fileId, DateCreated = DateTimeOffset.MinValue };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(false);

        // Act
        var result = controller.ProcessLock(fileId, wopiOverrideHeader);

        // Assert
        Assert.IsType<OkResult>(result);
        Assert.Equal(string.Empty, controller.Response.Headers[WopiHeaders.LOCK]);
    }

    [Fact]
    public void ProcessLock_LockWithoutOldLockIdentifier_ReturnsOkResult()
    {
        // Arrange
        var fileId = "test-file-id";
        var wopiOverrideHeader = WopiFileOperations.Lock;
        var newLockIdentifier = "new-lock-id";
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out It.Ref<WopiLockInfo?>.IsAny)).Returns(false);
        lockProviderMock.Setup(x => x.AddLock(fileId, newLockIdentifier)).Returns(new WopiLockInfo { LockId = newLockIdentifier, FileId = fileId });

        // Act
        var result = controller.ProcessLock(fileId, wopiOverrideHeader, newLockIdentifier: newLockIdentifier);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void ProcessLock_Unlock_ReturnsOkResult()
    {
        // Arrange
        var fileId = "test-file-id";
        var wopiOverrideHeader = WopiFileOperations.Unlock;
        var newLockIdentifier = "existing-lock-id";
        var lockInfo = new WopiLockInfo { LockId = newLockIdentifier, FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);
        lockProviderMock.Setup(x => x.RemoveLock(fileId)).Returns(true);

        // Act
        var result = controller.ProcessLock(fileId, wopiOverrideHeader, newLockIdentifier: newLockIdentifier);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void ProcessLock_RefreshLock_ReturnsOkResult()
    {
        // Arrange
        var fileId = "test-file-id";
        var wopiOverrideHeader = WopiFileOperations.RefreshLock;
        var newLockIdentifier = "existing-lock-id";
        var lockInfo = new WopiLockInfo { LockId = newLockIdentifier, FileId = fileId };
        lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);
        lockProviderMock.Setup(x => x.RefreshLock(fileId, null)).Returns(true);

        // Act
        var result = controller.ProcessLock(fileId, wopiOverrideHeader, newLockIdentifier: newLockIdentifier);

        // Assert
        Assert.IsType<OkResult>(result);
    }
}
