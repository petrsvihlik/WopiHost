namespace WopiHost.FileSystemProvider.Tests;

public class WopiFileSystemProviderOptionsTests
{
    [Fact]
    public void RootPath_RoundTrips()
    {
        var options = new WopiFileSystemProviderOptions { RootPath = "/some/path" };
        Assert.Equal("/some/path", options.RootPath);
    }
}
