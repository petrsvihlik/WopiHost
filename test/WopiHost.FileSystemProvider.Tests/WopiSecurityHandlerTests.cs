using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Moq;
using WopiHost.Abstractions;

namespace WopiHost.FileSystemProvider.Tests;

public class WopiSecurityHandlerTests
{
    private readonly WopiSecurityHandler _handler;

    public WopiSecurityHandlerTests()
    {
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        _handler = new WopiSecurityHandler(loggerFactory.Object);
    }

    [Fact]
    public async Task GetFilePermissions_ReturnsExpectedPermissions()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "user1")], "test"));
        var file = new Mock<IWopiFile>();

        // Act
        var result = await _handler.GetFilePermissions(principal, file.Object);

        // Assert
        Assert.True(result.HasFlag(WopiFilePermissions.UserCanWrite));
        Assert.True(result.HasFlag(WopiFilePermissions.UserCanRename));
        Assert.True(result.HasFlag(WopiFilePermissions.UserCanAttend));
        Assert.True(result.HasFlag(WopiFilePermissions.UserCanPresent));
        Assert.False(result.HasFlag(WopiFilePermissions.ReadOnly));
        Assert.False(result.HasFlag(WopiFilePermissions.WebEditingDisabled));
    }

    [Fact]
    public async Task GetContainerPermissions_ReturnsExpectedPermissions()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "user1")], "test"));
        var container = new Mock<IWopiFolder>();

        // Act
        var result = await _handler.GetContainerPermissions(principal, container.Object);

        // Assert
        Assert.True(result.HasFlag(WopiContainerPermissions.UserCanCreateChildContainer));
        Assert.True(result.HasFlag(WopiContainerPermissions.UserCanCreateChildFile));
        Assert.True(result.HasFlag(WopiContainerPermissions.UserCanDelete));
        Assert.True(result.HasFlag(WopiContainerPermissions.UserCanRename));
    }

    [Fact]
    public async Task IsAuthorized_ReturnsTrue_WhenUserIsAuthenticated()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "user1")], "test"));
        var requirement = new Mock<IWopiAuthorizationRequirement>();

        // Act
        var result = await _handler.IsAuthorized(principal, requirement.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAuthorized_ReturnsFalse_WhenUserIsNotAuthenticated()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var requirement = new Mock<IWopiAuthorizationRequirement>();

        // Act
        var result = await _handler.IsAuthorized(principal, requirement.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GenerateAccessToken_ReturnsValidToken()
    {
        // Act
        var token = await _handler.GenerateAccessToken("Anonymous", "resource1");

        // Assert
        Assert.NotNull(token);
        var tokenString = _handler.WriteToken(token);
        Assert.False(string.IsNullOrEmpty(tokenString));
    }

    [Fact]
    public async Task GetPrincipal_ReturnsNull_ForInvalidToken()
    {
        // Act
        var result = await _handler.GetPrincipal("invalid-token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPrincipal_ReturnsPrincipal_ForValidToken()
    {
        // Arrange
        var token = await _handler.GenerateAccessToken("Anonymous", "resource1");
        var tokenString = _handler.WriteToken(token);

        // Act
        var result = await _handler.GetPrincipal(tokenString);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Identity?.IsAuthenticated);
    }
}
