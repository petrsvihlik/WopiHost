using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Tests.Security.Authentication;

public class WopiOriginValidationActionFilterTests
{
    private readonly Mock<IWopiProofValidator> _validator = new();

    private static ActionExecutingContext BuildContext(HttpContext httpContext)
    {
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            controller: new object());
    }

    /// <summary>
    /// Constructs an HttpContext with an authenticated principal so the filter's pipeline-order
    /// guard passes and the test can exercise the post-auth code paths (access token, proof).
    /// </summary>
    private static DefaultHttpContext BuildAuthenticatedHttpContext()
    {
        var http = new DefaultHttpContext
        {
            // Identity authentication requires a non-null AuthenticationType for IsAuthenticated
            // to be true — DefaultHttpContext starts with an unauthenticated empty ClaimsIdentity.
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
        };
        return http;
    }

    private WopiOriginValidationActionFilter BuildSut() =>
        new(_validator.Object, NullLogger<WopiOriginValidationActionFilter>.Instance);

    [Fact]
    public async Task OnActionExecutionAsync_Unauthenticated_ReturnsUnauthorizedAndShortCircuits()
    {
        // Regression test for the pipeline-order invariant: if [Authorize] was removed or the
        // auth scheme is misregistered, the proof filter must reject — it has no validated
        // principal to anchor the proof signature against.
        var ctx = BuildContext(new DefaultHttpContext()); // unauthenticated by default
        var nextCalled = false;
        Task<ActionExecutedContext> Next() { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); }

        await BuildSut().OnActionExecutionAsync(ctx, Next);

        Assert.IsType<UnauthorizedResult>(ctx.Result);
        Assert.False(nextCalled);
        // Proof validator must never be called for an unauthenticated request — the guard runs first.
        _validator.Verify(v => v.ValidateProofAsync(It.IsAny<HttpContext>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoAccessToken_SetsInternalServerError()
    {
        var ctx = BuildContext(BuildAuthenticatedHttpContext());
        var nextCalled = false;
        Task<ActionExecutedContext> Next() { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); }

        await BuildSut().OnActionExecutionAsync(ctx, Next);

        var statusResult = Assert.IsType<StatusCodeResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ProofInvalid_SetsResultToInternalServerError()
    {
        var http = BuildAuthenticatedHttpContext();
        http.Request.QueryString = new QueryString("?access_token=abc");
        _validator
            .Setup(v => v.ValidateProofAsync(http, "abc"))
            .ReturnsAsync(false);
        var ctx = BuildContext(http);
        var nextCalled = false;
        Task<ActionExecutedContext> Next() { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); }

        await BuildSut().OnActionExecutionAsync(ctx, Next);

        var statusResult = Assert.IsType<StatusCodeResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ProofValid_CallsNext()
    {
        var http = BuildAuthenticatedHttpContext();
        http.Request.QueryString = new QueryString("?access_token=abc");
        _validator
            .Setup(v => v.ValidateProofAsync(http, "abc"))
            .ReturnsAsync(true);
        var ctx = BuildContext(http);
        var nextCalled = false;
        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(ctx, [], controller: new object()));
        }

        await BuildSut().OnActionExecutionAsync(ctx, Next);

        Assert.True(nextCalled);
        Assert.Null(ctx.Result);
    }
}
