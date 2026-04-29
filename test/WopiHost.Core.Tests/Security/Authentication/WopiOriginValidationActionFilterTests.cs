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

    private WopiOriginValidationActionFilter BuildSut() =>
        new(_validator.Object, NullLogger<WopiOriginValidationActionFilter>.Instance);

    [Fact]
    public async Task OnActionExecutionAsync_NoAccessToken_SetsInternalServerError()
    {
        var ctx = BuildContext(new DefaultHttpContext());
        var nextCalled = false;
        ActionExecutionDelegate next = () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); };

        await BuildSut().OnActionExecutionAsync(ctx, next);

        Assert.Equal(StatusCodes.Status500InternalServerError, ctx.HttpContext.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ProofInvalid_SetsResultToInternalServerError()
    {
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString("?access_token=abc");
        _validator
            .Setup(v => v.ValidateProofAsync(http, "abc"))
            .ReturnsAsync(false);
        var ctx = BuildContext(http);
        var nextCalled = false;
        ActionExecutionDelegate next = () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); };

        await BuildSut().OnActionExecutionAsync(ctx, next);

        var statusResult = Assert.IsType<StatusCodeResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ProofValid_CallsNext()
    {
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString("?access_token=abc");
        _validator
            .Setup(v => v.ValidateProofAsync(http, "abc"))
            .ReturnsAsync(true);
        var ctx = BuildContext(http);
        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(ctx, [], controller: new object()));
        };

        await BuildSut().OnActionExecutionAsync(ctx, next);

        Assert.True(nextCalled);
        Assert.Null(ctx.Result);
    }
}
