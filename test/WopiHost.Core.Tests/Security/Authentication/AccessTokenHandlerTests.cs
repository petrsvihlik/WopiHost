using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Tests.Security.Authentication;

public class AccessTokenHandlerTests
{
    private readonly Mock<IWopiSecurityHandler> _securityHandlerMock;
    private readonly Mock<UrlEncoder> _urlEncoderMock;
    private readonly AccessTokenHandler _accessTokenHandler;
    private readonly AuthenticationScheme scheme;

    public AccessTokenHandlerTests()
    {
        _securityHandlerMock = new Mock<IWopiSecurityHandler>();
        var options = new Mock<IOptionsMonitor<AccessTokenAuthenticationOptions>>();
        options
            .Setup(x => x.Get(It.IsAny<string>()))
            .Returns(new AccessTokenAuthenticationOptions());
        var logger = new Mock<ILogger<AccessTokenHandler>>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        _urlEncoderMock = new Mock<UrlEncoder>();

        scheme = new AuthenticationScheme(
            AccessTokenDefaults.AUTHENTICATION_SCHEME,
            null,
            typeof(AccessTokenHandler));

        _accessTokenHandler = new AccessTokenHandler(
            _securityHandlerMock.Object,
            options.Object,
            loggerFactory.Object,
            _urlEncoderMock.Object
        );
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldReturnNoResult_WhenNotWopiPath()
    {
        // Arrange
        var context = new DefaultHttpContext()
        {
            Request =
            {
                Path = "/whatever",
            }
        };
        await _accessTokenHandler.InitializeAsync(scheme, context);

        // Act
        var result = await _accessTokenHandler.AuthenticateAsync();

        // Assert
        Assert.True(result.None);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldReturnSuccess_WhenTokenIsValid()
    {
        // Arrange
        var token = "valid_token";
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        _securityHandlerMock
            .Setup(x => x.GetPrincipal(token, default))
            .ReturnsAsync(principal);
        var context = new DefaultHttpContext()
        {
            Request =
            {
                Path = "/wopi",
                Query = new QueryCollection(new Dictionary<string, StringValues>
                {
                    { AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME, token }
                })
            }
        };
        await _accessTokenHandler.InitializeAsync(scheme, context);

        // Act
        var result = await _accessTokenHandler.AuthenticateAsync();

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldReturnFailure_WhenTokenIsInvalid()
    {
        // Arrange
        var token = "invalid_token";
        _securityHandlerMock
            .Setup(x => x.GetPrincipal(token, default))
            .ReturnsAsync((ClaimsPrincipal?)null);
        var context = new DefaultHttpContext()
        {
            Request =
            {
                Query = new QueryCollection(new Dictionary<string, StringValues>
                {
                    { AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME, token }
                })
            }
        };
        await _accessTokenHandler.InitializeAsync(scheme, context);

        // Act
        var result = await _accessTokenHandler.AuthenticateAsync();

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldReturnFailure_WhenTokenIsMissing()
    {
        // Arrange
        var context = new DefaultHttpContext();
        await _accessTokenHandler.InitializeAsync(scheme, context);

        // Act
        var result = await _accessTokenHandler.AuthenticateAsync();

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldReturnFailure_WhenExceptionIsThrown()
    {
        // Arrange
        var token = "valid_token";
        _securityHandlerMock
            .Setup(x => x.GetPrincipal(token, default))
            .ThrowsAsync(new Exception("Test exception"));
        var context = new DefaultHttpContext()
        {
            Request =
            {
                Query = new QueryCollection(new Dictionary<string, StringValues>
                {
                    { AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME, token }
                })
            }
        };
        await _accessTokenHandler.InitializeAsync(scheme, context);

        // Act
        var result = await _accessTokenHandler.AuthenticateAsync();

        // Assert
        Assert.False(result.Succeeded);
    }
}
