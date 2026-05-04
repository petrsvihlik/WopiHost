using System.Xml;
using WopiHost.Discovery;

namespace WopiHost.Discovery.Tests;

public class FileSystemDiscoveryFileProviderTests
{
    [Fact]
    public async Task GetDiscoveryXmlAsync_ValidFile_ReturnsXml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "OOS2016_discovery.xml");
        var sut = new FileSystemDiscoveryFileProvider(path);

        var xml = await sut.GetDiscoveryXmlAsync();

        Assert.NotNull(xml);
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_MissingFile_ThrowsAndIsObservableViaCatchPath()
    {
        var sut = new FileSystemDiscoveryFileProvider(Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid()}.xml"));

        await Assert.ThrowsAnyAsync<IOException>(() => sut.GetDiscoveryXmlAsync());
    }

    [Fact]
    public async Task GetDiscoveryXmlAsync_MalformedXml_ThrowsXmlException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"malformed-{Guid.NewGuid()}.xml");
        await File.WriteAllTextAsync(path, "<not-closed-element>");
        try
        {
            var sut = new FileSystemDiscoveryFileProvider(path);

            await Assert.ThrowsAsync<XmlException>(() => sut.GetDiscoveryXmlAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
