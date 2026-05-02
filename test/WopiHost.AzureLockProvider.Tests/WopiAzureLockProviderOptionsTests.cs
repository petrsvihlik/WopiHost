using Xunit;

namespace WopiHost.AzureLockProvider.Tests;

public class WopiAzureLockProviderOptionsTests
{
    [Fact]
    public void Defaults_PopulateContainerName_LeaveAuthEmpty()
    {
        var options = new WopiAzureLockProviderOptions();

        Assert.Null(options.ConnectionString);
        Assert.Null(options.ServiceUri);
        Assert.Equal("wopi-locks", options.ContainerName);
    }

    [Fact]
    public void Setters_RoundTripValues()
    {
        var options = new WopiAzureLockProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ServiceUri = "https://acct.blob.core.windows.net",
            ContainerName = "custom-locks",
        };

        Assert.Equal("UseDevelopmentStorage=true", options.ConnectionString);
        Assert.Equal("https://acct.blob.core.windows.net", options.ServiceUri);
        Assert.Equal("custom-locks", options.ContainerName);
    }
}
