using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace WopiHost.Core.Tests;

public static class TestUtils
{
    public static IServiceScopeFactory CreateServiceScope()
    {
        var serviceProvider = new Mock<IServiceProvider>();

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        serviceScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(serviceScope.Object);

        return serviceScopeFactory.Object;
    }


    public static IServiceScopeFactory CreateServiceScope<T1>(T1 instance1)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(x => x.GetService(typeof(T1)))
            .Returns(instance1);

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        serviceScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(serviceScope.Object);

        return serviceScopeFactory.Object;
    }
}
