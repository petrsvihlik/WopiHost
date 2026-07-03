namespace WopiHost.FileSystemProvider.Tests;

public class WopiFileSystemProviderOptionsTests
{
    [Fact]
    public void RootPath_RoundTrips()
    {
        var options = new WopiFileSystemProviderOptions { RootPath = "/some/path" };
        Assert.Equal("/some/path", options.RootPath);
    }

    [Fact]
    public void WatchForExternalChanges_DefaultsToTrue()
    {
        var options = new WopiFileSystemProviderOptions { RootPath = "/some/path" };
        Assert.True(options.WatchForExternalChanges);
    }
}
