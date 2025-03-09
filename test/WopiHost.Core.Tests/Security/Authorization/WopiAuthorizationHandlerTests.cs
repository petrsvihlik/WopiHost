using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Tests.Security.Authorization;

public class WopiAuthorizationHandlerTests
{
    private readonly Mock<IWopiSecurityHandler> _mockSecurityHandler;
    private readonly WopiAuthorizationHandler _handler;
    private readonly WopiAuthorizeAttribute _requirement;

    public WopiAuthorizationHandlerTests()
    {
        _mockSecurityHandler = new Mock<IWopiSecurityHandler>();
        _handler = new WopiAuthorizationHandler(_mockSecurityHandler.Object);
        _requirement = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Read);
    }

    [Fact]
    public async Task HandleRequirementAsync_ShouldSucceed_WhenUserIsAuthorized()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["id"] = "fileId";
        var context = new AuthorizationHandlerContext([_requirement], new ClaimsPrincipal(), httpContext);
        _mockSecurityHandler
            .Setup(s => s.IsAuthorized(It.IsAny<ClaimsPrincipal>(), _requirement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_ShouldFail_WhenUserIsNotAuthorized()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["id"] = "fileId";
        var context = new AuthorizationHandlerContext([_requirement], new ClaimsPrincipal(), httpContext);
        _mockSecurityHandler
            .Setup(s => s.IsAuthorized(It.IsAny<ClaimsPrincipal>(), _requirement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_ShouldSetResourceId_WhenRouteValueExists()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["id"] = "fileId";
        var context = new AuthorizationHandlerContext([_requirement], new ClaimsPrincipal(), httpContext);
        _mockSecurityHandler
            .Setup(s => s.IsAuthorized(It.IsAny<ClaimsPrincipal>(), _requirement, It.IsAny<CancellationToken>()))
            .Callback<ClaimsPrincipal, IWopiAuthorizationRequirement, CancellationToken>((p, r, c) => Assert.Equal("fileId", r.ResourceId))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_ShouldNotSetResourceId_WhenRouteValueDoesNotExist()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var context = new AuthorizationHandlerContext([_requirement], new ClaimsPrincipal(), httpContext);
        _mockSecurityHandler
            .Setup(s => s.IsAuthorized(It.IsAny<ClaimsPrincipal>(), _requirement, It.IsAny<CancellationToken>()))
            .Callback<ClaimsPrincipal, IWopiAuthorizationRequirement, CancellationToken>((p, r, c) => Assert.Null(r.ResourceId))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_ShouldAddPermissionsToHttpContextItems()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["id"] = "fileId";
        _requirement.CheckPermissions = [Permission.Read, Permission.Update];
        var context = new AuthorizationHandlerContext([_requirement], new ClaimsPrincipal(), httpContext);
        _mockSecurityHandler
            .Setup(s => s.IsAuthorized(It.IsAny<ClaimsPrincipal>(), _requirement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(httpContext.Items.ContainsKey(Permission.Read));
        Assert.True(httpContext.Items.ContainsKey(Permission.Update));
    }
}
