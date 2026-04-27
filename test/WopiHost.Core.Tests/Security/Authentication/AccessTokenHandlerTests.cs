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
    private readonly Mock<IWopiAccessTokenService> _accessTokenServiceMock = new();
    private readonly AccessTokenHandler _handler;
    private readonly AuthenticationScheme _scheme;

    public AccessTokenHandlerTests()
    {
        var options = new Mock<IOptionsMonitor<AccessTokenAuthenticationOptions>>();
        options.Setup(x => x.Get(It.IsAny<string>())).Returns(new AccessTokenAuthenticationOptions());
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger<AccessTokenHandler>>().Object);

        _scheme = new AuthenticationScheme(WopiAuthenticationSchemes.AccessToken, null, typeof(AccessTokenHandler));
        _handler = new AccessTokenHandler(_accessTokenServiceMock.Object, options.Object, loggerFactory.Object, new Mock<UrlEncoder>().Object);
    }

    private static DefaultHttpContext BuildContext(string path, string? token)
    {
        var ctx = new DefaultHttpContext { Request = { Path = path } };
        if (token is not null)
        {
            ctx.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
            {
                { AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME, token }
            });
        }
        return ctx;
    }

    [Fact]
    public async Task Returns_NoResult_When_Path_Is_Not_Wopi()
    {
        var context = BuildContext("/whatever", token: null);
        await _handler.InitializeAsync(_scheme, context);

        var result = await _handler.AuthenticateAsync();

        Assert.True(result.None);
    }

    [Fact]
    public async Task Fails_When_Token_Is_Missing()
    {
        var context = BuildContext("/wopi/files/abc", token: null);
        await _handler.InitializeAsync(_scheme, context);

        var result = await _handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Succeeds_When_AccessTokenService_Validates()
    {
        const string token = "valid-token";
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        _accessTokenServiceMock
            .Setup(x => x.ValidateAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiAccessTokenValidationResult.Success(principal));

        var context = BuildContext("/wopi/files/abc", token);
        await _handler.InitializeAsync(_scheme, context);

        var result = await _handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Same(principal, result.Principal);
    }

    [Fact]
    public async Task Fails_When_AccessTokenService_Returns_Failure()
    {
        const string token = "expired-token";
        _accessTokenServiceMock
            .Setup(x => x.ValidateAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiAccessTokenValidationResult.Failure("expired"));

        var context = BuildContext("/wopi/files/abc", token);
        await _handler.InitializeAsync(_scheme, context);

        var result = await _handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("expired", result.Failure?.Message);
    }

    [Fact]
    public async Task Stores_Token_In_AuthenticationProperties_When_SaveToken_True()
    {
        // SaveToken defaults to true (see AccessTokenAuthenticationOptions); the existing
        // _handler uses that default. After a successful authentication the raw token
        // string should be retrievable via GetTokenAsync(...) on the principal's properties.
        const string token = "valid-token";
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        _accessTokenServiceMock
            .Setup(x => x.ValidateAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiAccessTokenValidationResult.Success(principal));

        var context = BuildContext("/wopi/files/abc", token);
        await _handler.InitializeAsync(_scheme, context);

        var result = await _handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        var saved = result.Properties?.GetTokenValue(AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME);
        Assert.Equal(token, saved);
    }

    [Fact]
    public async Task Does_Not_Store_Token_When_SaveToken_False()
    {
        const string token = "valid-token";
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        _accessTokenServiceMock
            .Setup(x => x.ValidateAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WopiAccessTokenValidationResult.Success(principal));

        // Build a fresh handler whose options say SaveToken = false.
        var optionsMonitor = new Mock<IOptionsMonitor<AccessTokenAuthenticationOptions>>();
        optionsMonitor.Setup(x => x.Get(It.IsAny<string>())).Returns(new AccessTokenAuthenticationOptions { SaveToken = false });
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger<AccessTokenHandler>>().Object);
        var handler = new AccessTokenHandler(_accessTokenServiceMock.Object, optionsMonitor.Object, loggerFactory.Object, new Mock<UrlEncoder>().Object);

        var context = BuildContext("/wopi/files/abc", token);
        await handler.InitializeAsync(_scheme, context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        var saved = result.Properties?.GetTokenValue(AccessTokenDefaults.ACCESS_TOKEN_QUERY_NAME);
        Assert.Null(saved);
    }

    [Fact]
    public async Task Falls_Back_To_Generic_Failure_Message_When_Validator_Returns_Null_Reason()
    {
        const string token = "bad-token";
        // Construct a Failure result manually with FailureReason=null to exercise the
        // null-coalescing path in AccessTokenHandler.
        _accessTokenServiceMock
            .Setup(x => x.ValidateAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WopiAccessTokenValidationResult(false, null, null));

        var context = BuildContext("/wopi/files/abc", token);
        await _handler.InitializeAsync(_scheme, context);

        var result = await _handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid access token.", result.Failure?.Message);
    }
}
