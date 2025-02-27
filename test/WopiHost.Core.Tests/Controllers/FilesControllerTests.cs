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
    private readonly Mock<IWopiStorageProvider> _storageProviderMock;
    private readonly Mock<IWopiSecurityHandler> _securityHandlerMock;
    private readonly Mock<IOptions<WopiHostOptions>> _wopiHostOptionsMock;
    private readonly Mock<IAuthorizationService> _authorizationServiceMock;
    private readonly Mock<IWopiLockProvider> _lockProviderMock;
    private FilesController _controller;

    public FilesControllerTests()
    {
        _storageProviderMock = new Mock<IWopiStorageProvider>();
        _securityHandlerMock = new Mock<IWopiSecurityHandler>();
        _wopiHostOptionsMock = new Mock<IOptions<WopiHostOptions>>();
        _authorizationServiceMock = new Mock<IAuthorizationService>();
        _lockProviderMock = new Mock<IWopiLockProvider>();

        _controller = new FilesController(
            _storageProviderMock.Object,
            _securityHandlerMock.Object,
            _wopiHostOptionsMock.Object,
            _authorizationServiceMock.Object,
            _lockProviderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void ProcessLock_LockingNotSupported_ReturnsLockMismatchResult()
    {
        // Arrange
        _controller = new FilesController(
            _storageProviderMock.Object,
            _securityHandlerMock.Object,
            _wopiHostOptionsMock.Object,
            _authorizationServiceMock.Object,
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
        var result = _controller.ProcessLock(fileId, wopiOverrideHeader);

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
        _lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);

        // Act
        var result = _controller.ProcessLock(fileId, wopiOverrideHeader);

        // Assert
        Assert.IsType<OkResult>(result);
        Assert.Equal("existing-lock-id", _controller.Response.Headers[WopiHeaders.LOCK]);
    }

    [Fact]
    public void ProcessLock_GetLock_NoLockInfo_ReturnsLockMismatchResult()
    {
        // Arrange
        var fileId = "test-file-id";
        var wopiOverrideHeader = WopiFileOperations.GetLock;
        WopiLockInfo? lockInfo = null;
        _lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);

        // Act
        var result = _controller.ProcessLock(fileId, wopiOverrideHeader);

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
        _lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(false);

        // Act
        var result = _controller.ProcessLock(fileId, wopiOverrideHeader);

        // Assert
        Assert.IsType<OkResult>(result);
        Assert.Equal(string.Empty, _controller.Response.Headers[WopiHeaders.LOCK]);
    }

    [Fact]
    public void ProcessLock_LockWithoutOldLockIdentifier_ReturnsOkResult()
    {
        // Arrange
        var fileId = "test-file-id";
        var wopiOverrideHeader = WopiFileOperations.Lock;
        var newLockIdentifier = "new-lock-id";
        _lockProviderMock.Setup(x => x.TryGetLock(fileId, out It.Ref<WopiLockInfo?>.IsAny)).Returns(false);
        _lockProviderMock.Setup(x => x.AddLock(fileId, newLockIdentifier)).Returns(new WopiLockInfo { LockId = newLockIdentifier, FileId = fileId });

        // Act
        var result = _controller.ProcessLock(fileId, wopiOverrideHeader, newLockIdentifier: newLockIdentifier);

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
        _lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);
        _lockProviderMock.Setup(x => x.RemoveLock(fileId)).Returns(true);

        // Act
        var result = _controller.ProcessLock(fileId, wopiOverrideHeader, newLockIdentifier: newLockIdentifier);

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
        _lockProviderMock.Setup(x => x.TryGetLock(fileId, out lockInfo)).Returns(true);
        _lockProviderMock.Setup(x => x.RefreshLock(fileId, null)).Returns(true);

        // Act
        var result = _controller.ProcessLock(fileId, wopiOverrideHeader, newLockIdentifier: newLockIdentifier);

        // Assert
        Assert.IsType<OkResult>(result);
    }
}
