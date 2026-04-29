using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WopiHost.Core.Controllers;
using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

public class WopiRouteNamesTests
{
    private readonly IReadOnlyList<ActionDescriptor> actionDescriptors;

    public WopiRouteNamesTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddMvcCore()
            .AddApplicationPart(typeof(FilesController).Assembly);
        services.AddRouting();

        var provider = services.BuildServiceProvider();
        actionDescriptors = provider.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items;
    }

    [Theory]
    [InlineData(WopiRouteNames.CheckFileInfo, "wopi/Files/{id}")]
    [InlineData(WopiRouteNames.CheckContainerInfo, "wopi/Containers/{id}")]
    [InlineData(WopiRouteNames.CheckFolderInfo, "wopi/Folders/{id}")]
    [InlineData(WopiRouteNames.CheckEcosystem, "wopi/Ecosystem")]
    public void NamedRoute_HasExpectedTemplate(string routeName, string expectedTemplate)
    {
        var descriptor = actionDescriptors
            .FirstOrDefault(d => d.AttributeRouteInfo?.Name == routeName);

        Assert.NotNull(descriptor);
        Assert.Equal(expectedTemplate, descriptor.AttributeRouteInfo!.Template);
    }
}
