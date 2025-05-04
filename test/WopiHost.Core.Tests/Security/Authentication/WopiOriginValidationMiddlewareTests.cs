using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Tests.Security.Authentication;

public class WopiOriginValidationMiddlewareTests
{
    private readonly Mock<IWopiProofValidator> _mockValidator;
    private readonly Mock<ILogger<WopiOriginValidationMiddleware>> _mockLogger;
    private readonly WopiOriginValidationMiddleware _middleware;
    private readonly RequestDelegate _nextMiddleware;
    private bool _nextCalled;

    public WopiOriginValidationMiddlewareTests()
    {
        _mockValidator = new Mock<IWopiProofValidator>();
        _mockLogger = new Mock<ILogger<WopiOriginValidationMiddleware>>();
        _middleware = new WopiOriginValidationMiddleware(_mockValidator.Object, _mockLogger.Object);
        
        _nextCalled = false;
        _nextMiddleware = (HttpContext context) => 
        {
            _nextCalled = true;
            return Task.CompletedTask;
        };
    }
    
    [Fact]
    public async Task InvokeAsync_WhenProofValidationSucceeds_ShouldCallNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var request = context.Request;
        
        // Add required headers
        request.Headers[WopiHeaders.PROOF] = "valid-proof";
        request.Headers[WopiHeaders.TIMESTAMP] = "123456789";
        
        const string accessToken = "test-access-token";
        
        // Setup extension method behavior through query string
        request.QueryString = new QueryString($"?access_token={accessToken}");
        
        // Setup validator to return success
        _mockValidator
            .Setup(v => v.ValidateProofAsync(request, accessToken))
            .ReturnsAsync(true);
        
        // Act
        await _middleware.InvokeAsync(context, _nextMiddleware);
        
        // Assert
        Assert.True(_nextCalled, "Next middleware should have been called");
        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
    }
    
    [Fact]
    public async Task InvokeAsync_WhenProofValidationFails_ShouldReturn500()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var request = context.Request;
        
        // Add required headers
        request.Headers[WopiHeaders.PROOF] = "invalid-proof";
        request.Headers[WopiHeaders.TIMESTAMP] = "123456789";
        
        const string accessToken = "test-access-token";
        
        // Setup extension method behavior through query string
        request.QueryString = new QueryString($"?access_token={accessToken}");
        
        // Setup validator to return failure
        _mockValidator
            .Setup(v => v.ValidateProofAsync(request, accessToken))
            .ReturnsAsync(false);
        
        // Act
        await _middleware.InvokeAsync(context, _nextMiddleware);
        
        // Assert
        Assert.False(_nextCalled, "Next middleware should not have been called");
        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
    }
    
    [Fact]
    public async Task InvokeAsync_WhenProofHeaderMissing_ShouldReturn500()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var request = context.Request;
        
        // Add only timestamp header, missing proof
        request.Headers[WopiHeaders.TIMESTAMP] = "123456789";
        
        // Setup extension method behavior through query string
        request.QueryString = new QueryString("?access_token=test-access-token");
        
        // Act
        await _middleware.InvokeAsync(context, _nextMiddleware);
        
        // Assert
        Assert.False(_nextCalled, "Next middleware should not have been called");
        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
    }
    
    [Fact]
    public async Task InvokeAsync_WhenTimestampHeaderMissing_ShouldReturn500()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var request = context.Request;
        
        // Add only proof header, missing timestamp
        request.Headers[WopiHeaders.PROOF] = "valid-proof";
        
        // Setup extension method behavior through query string
        request.QueryString = new QueryString("?access_token=test-access-token");
        
        // Act
        await _middleware.InvokeAsync(context, _nextMiddleware);
        
        // Assert
        Assert.False(_nextCalled, "Next middleware should not have been called");
        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
    }
    
    [Fact]
    public async Task InvokeAsync_WhenAccessTokenMissing_ShouldReturn500()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var request = context.Request;
        
        // Add required headers
        request.Headers[WopiHeaders.PROOF] = "valid-proof";
        request.Headers[WopiHeaders.TIMESTAMP] = "123456789";
        
        // Empty query string - no access token
        request.QueryString = new QueryString("");
        
        // Act
        await _middleware.InvokeAsync(context, _nextMiddleware);
        
        // Assert
        Assert.False(_nextCalled, "Next middleware should not have been called");
        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
    }
} 