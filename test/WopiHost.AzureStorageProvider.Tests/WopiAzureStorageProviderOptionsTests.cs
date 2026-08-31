using Xunit;

namespace WopiHost.AzureStorageProvider.Tests;

public class WopiAzureStorageProviderOptionsTests
{
    [Fact]
    public void Defaults_AreNullExceptRequired()
    {
        var options = new WopiAzureStorageProviderOptions { ContainerName = "x" };

        Assert.Null(options.ConnectionString);
        Assert.Null(options.ServiceUri);
        Assert.Equal("x", options.ContainerName);
    }

}
