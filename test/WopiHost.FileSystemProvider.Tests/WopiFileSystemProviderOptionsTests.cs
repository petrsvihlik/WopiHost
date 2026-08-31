namespace WopiHost.FileSystemProvider.Tests;

public class WopiFileSystemProviderOptionsTests
{
    [Fact]
    public void WatchForExternalChanges_DefaultsToTrue()
    {
        var options = new WopiFileSystemProviderOptions { RootPath = "/some/path" };
        Assert.True(options.WatchForExternalChanges);
    }
}
