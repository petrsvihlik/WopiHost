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
}
