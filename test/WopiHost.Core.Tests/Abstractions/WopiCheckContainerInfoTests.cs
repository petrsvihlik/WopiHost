using WopiHost.Abstractions;

namespace WopiHost.Core.Tests.Abstractions;

public class WopiCheckContainerInfoTests
{
    [Fact]
    public void OptionalProperties_RoundTrip()
    {
        var hostUrl = new Uri("https://host/container");
        var sharingUrl = new Uri("https://host/share");

        var sut = new WopiCheckContainerInfo
        {
            Name = "folder",
            HostUrl = hostUrl,
            LicenseCheckForEditIsEnabled = true,
            SharingUrl = sharingUrl,
        };

        Assert.Equal("folder", sut.Name);
        Assert.Equal(hostUrl, sut.HostUrl);
        Assert.True(sut.LicenseCheckForEditIsEnabled);
        Assert.Equal(sharingUrl, sut.SharingUrl);
    }
}
