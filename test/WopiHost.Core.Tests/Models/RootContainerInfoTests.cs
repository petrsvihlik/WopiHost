using WopiHost.Abstractions;
using WopiHost.Core.Models;

namespace WopiHost.Core.Tests.Models;

public class RootContainerInfoTests
{
    [Fact]
    public void ContainerInfo_RoundTrips()
    {
        var info = new WopiCheckContainerInfo { Name = "Root" };
        var sut = new RootContainerInfo
        {
            ContainerPointer = new ChildContainer("Root", "https://wopi/root"),
            ContainerInfo = info,
        };

        Assert.Same(info, sut.ContainerInfo);
    }
}
