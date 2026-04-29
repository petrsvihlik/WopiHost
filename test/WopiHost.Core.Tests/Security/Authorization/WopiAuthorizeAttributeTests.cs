using WopiHost.Abstractions;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Tests.Security.Authorization;

public class WopiAuthorizeAttributeTests
{
    [Fact]
    public void Constructor_StoresResourceTypeAndPermission()
    {
        var sut = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Read);

        Assert.Equal(WopiResourceType.File, sut.ResourceType);
        Assert.Equal(Permission.Read, sut.Permission);
    }

    [Fact]
    public void GetRequirements_YieldsSelf()
    {
        var sut = new WopiAuthorizeAttribute(WopiResourceType.Container, Permission.Read);

        var reqs = sut.GetRequirements().ToList();

        Assert.Single(reqs);
        Assert.Same(sut, reqs[0]);
    }

    [Fact]
    public void ResourceId_RoundTrips()
    {
        var sut = new WopiAuthorizeAttribute(WopiResourceType.File, Permission.Read)
        {
            ResourceId = "abc",
        };

        Assert.Equal("abc", sut.ResourceId);
    }
}
