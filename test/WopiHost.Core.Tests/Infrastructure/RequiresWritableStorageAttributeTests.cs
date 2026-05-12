using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Results;

namespace WopiHost.Core.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="RequiresWritableStorageAttribute"/>. The filter short-circuits actions
/// with HTTP 501 when no <see cref="IWopiWritableStorageProvider"/> is registered in the request
/// scope. These tests replace the per-action <c>*_NoWritableStorageProvider_*</c> unit tests that
/// previously lived on each controller — those exercised an inline if-check that has since moved
/// into this filter, so the precondition is now tested in one place.
/// </summary>
public sealed class RequiresWritableStorageAttributeTests
{
    [Fact]
    public async Task NoProviderRegistered_ShortCircuitsWith501AndDoesNotInvokeNext()
    {
        var sut = new RequiresWritableStorageAttribute();
        var context = BuildContext(services: new ServiceCollection().BuildServiceProvider());
        var nextInvoked = false;

        await sut.OnActionExecutionAsync(context, () =>
        {
            nextInvoked = true;
            return Task.FromResult(new ActionExecutedContext(context, context.Filters, context.Controller));
        });

        Assert.IsType<NotImplementedResult>(context.Result);
        Assert.False(nextInvoked, "next() must not run when the writable provider is missing.");
    }

    [Fact]
    public async Task ProviderRegistered_InvokesNextAndLeavesResultUnset()
    {
        var sut = new RequiresWritableStorageAttribute();
        var services = new ServiceCollection()
            .AddSingleton(Mock.Of<IWopiWritableStorageProvider>())
            .BuildServiceProvider();
        var context = BuildContext(services);
        var nextInvoked = false;

        await sut.OnActionExecutionAsync(context, () =>
        {
            nextInvoked = true;
            return Task.FromResult(new ActionExecutedContext(context, context.Filters, context.Controller));
        });

        Assert.True(nextInvoked, "next() must run when the writable provider is registered.");
        Assert.Null(context.Result);
    }

    private static ActionExecutingContext BuildContext(IServiceProvider services)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            filters: [],
            actionArguments: new Dictionary<string, object?>(),
            controller: new object());
    }
}
