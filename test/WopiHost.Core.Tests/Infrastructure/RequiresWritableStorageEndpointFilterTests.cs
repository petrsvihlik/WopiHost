using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

public class RequiresWritableStorageEndpointFilterTests
{
    [Fact]
    public async Task Short_Circuits_With_501_When_Writable_Provider_Missing()
    {
        var ctx = CreateContext(services => { /* no provider */ });
        var filter = new RequiresWritableStorageEndpointFilter();
        var nextCalled = false;
        ValueTask<object?> Next(EndpointFilterInvocationContext _) { nextCalled = true; return ValueTask.FromResult<object?>(null); }

        var result = await filter.InvokeAsync(ctx, Next);

        Assert.False(nextCalled);
        var status = Assert.IsType<IStatusCodeHttpResult>(result, exactMatch: false);
        Assert.Equal(StatusCodes.Status501NotImplemented, status.StatusCode);
    }

    [Fact]
    public async Task Calls_Next_When_Writable_Provider_Registered()
    {
        var writable = Mock.Of<IWopiWritableStorageProvider>();
        var ctx = CreateContext(services => services.AddSingleton(writable));
        var filter = new RequiresWritableStorageEndpointFilter();
        var sentinel = new object();
        ValueTask<object?> Next(EndpointFilterInvocationContext _) => ValueTask.FromResult<object?>(sentinel);

        var result = await filter.InvokeAsync(ctx, Next);

        Assert.Same(sentinel, result);
    }

    private static DefaultEndpointFilterInvocationContext CreateContext(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        return new DefaultEndpointFilterInvocationContext(httpContext);
    }
}
