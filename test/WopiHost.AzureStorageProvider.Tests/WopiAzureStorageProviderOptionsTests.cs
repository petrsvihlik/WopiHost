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

    [Fact]
    public void Setters_RoundTripValues()
    {
        var options = new WopiAzureStorageProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ServiceUri = "https://acct.blob.core.windows.net",
            ContainerName = "wopi-files",
        };

        Assert.Equal("UseDevelopmentStorage=true", options.ConnectionString);
        Assert.Equal("https://acct.blob.core.windows.net", options.ServiceUri);
        Assert.Equal("wopi-files", options.ContainerName);
    }
}
