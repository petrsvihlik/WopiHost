using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Tests.Security.Authentication;

public class WopiOriginValidationEndpointFilterTests
{
    [Fact]
    public async Task Returns_Unauthorized_When_User_Not_Authenticated()
    {
        var ctx = CreateContext(authenticated: false, accessToken: null);
        var validator = new Mock<IWopiProofValidator>(MockBehavior.Strict);
        var filter = new WopiOriginValidationEndpointFilter(validator.Object, NullLogger<WopiOriginValidationEndpointFilter>.Instance);

        var result = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(null));

        Assert.IsType<UnauthorizedHttpResult>(result);
        validator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Returns_500_When_Access_Token_Missing()
    {
        var ctx = CreateContext(authenticated: true, accessToken: null);
        var validator = new Mock<IWopiProofValidator>(MockBehavior.Strict);
        var filter = new WopiOriginValidationEndpointFilter(validator.Object, NullLogger<WopiOriginValidationEndpointFilter>.Instance);

        var result = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(null));

        var status = Assert.IsType<IStatusCodeHttpResult>(result, exactMatch: false);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public async Task Returns_500_When_Proof_Validation_Fails()
    {
        var ctx = CreateContext(authenticated: true, accessToken: "abc");
        var validator = new Mock<IWopiProofValidator>(MockBehavior.Strict);
        validator.Setup(v => v.ValidateProofAsync(ctx.HttpContext, "abc")).ReturnsAsync(false);
        var filter = new WopiOriginValidationEndpointFilter(validator.Object, NullLogger<WopiOriginValidationEndpointFilter>.Instance);

        var result = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(null));

        var status = Assert.IsType<IStatusCodeHttpResult>(result, exactMatch: false);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public async Task Calls_Next_When_Proof_Valid()
    {
        var ctx = CreateContext(authenticated: true, accessToken: "abc");
        var validator = new Mock<IWopiProofValidator>(MockBehavior.Strict);
        validator.Setup(v => v.ValidateProofAsync(ctx.HttpContext, "abc")).ReturnsAsync(true);
        var filter = new WopiOriginValidationEndpointFilter(validator.Object, NullLogger<WopiOriginValidationEndpointFilter>.Instance);
        var sentinel = new object();

        var result = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(sentinel));

        Assert.Same(sentinel, result);
    }

    private static DefaultEndpointFilterInvocationContext CreateContext(bool authenticated, string? accessToken)
    {
        var httpContext = new DefaultHttpContext();
        if (authenticated)
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u")], "test");
            httpContext.User = new ClaimsPrincipal(identity);
        }
        if (!string.IsNullOrEmpty(accessToken))
        {
            httpContext.Request.QueryString = new QueryString($"?access_token={accessToken}");
        }
        return new DefaultEndpointFilterInvocationContext(httpContext);
    }
}
