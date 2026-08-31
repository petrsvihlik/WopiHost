using System.Text.Json;
using WopiHost.Abstractions;

namespace WopiHost.Core.Tests.Abstractions;

public class WopiCheckContainerInfoTests
{
    [Fact]
    public void Serialization_EmitsWopiWireNamesAndValues()
    {
        // Pins the CheckContainerInfo wire contract: WOPI property names are PascalCase
        // (CheckContainerInfo goes out via TypedResults.Json with PropertyNamingPolicy = null,
        // which matches default-options serialization).
        var sut = new WopiCheckContainerInfo
        {
            Name = "folder",
            HostUrl = new Uri("https://host/container"),
            LicenseCheckForEditIsEnabled = true,
            SharingUrl = new Uri("https://host/share"),
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(sut));
        var root = doc.RootElement;

        Assert.Equal("folder", root.GetProperty("Name").GetString());
        Assert.Equal("https://host/container", root.GetProperty("HostUrl").GetString());
        Assert.True(root.GetProperty("LicenseCheckForEditIsEnabled").GetBoolean());
        Assert.Equal("https://host/share", root.GetProperty("SharingUrl").GetString());
        Assert.False(root.GetProperty("UserCanCreateChildContainer").GetBoolean());
    }
}
