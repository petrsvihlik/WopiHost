using System.Xml.Linq;
using FakeItEasy;
using Microsoft.Extensions.Options;

namespace WopiHost.Discovery.Tests;

public class WopiDiscovererProofKeyTests
{
    private static WopiDiscoverer CreateSut(XElement discoveryXml)
    {
        var fileProvider = A.Fake<IDiscoveryFileProvider>();
        A.CallTo(() => fileProvider.GetDiscoveryXmlAsync()).Returns(Task.FromResult(discoveryXml));
        return new WopiDiscoverer(fileProvider, Options.Create(new DiscoveryOptions()));
    }

    [Fact]
    public async Task GetProofKeysAsync_DiscoveryHasProofKey_ReturnsParsedKeys()
    {
        var xml = XElement.Parse(
            """
            <wopi-discovery>
                <proof-key value="v" oldvalue="ov" modulus="m" exponent="e" oldmodulus="om" oldexponent="oe" />
            </wopi-discovery>
            """);
        var sut = CreateSut(xml);

        var keys = await sut.GetProofKeysAsync();

        Assert.Equal("v", keys.Value);
        Assert.Equal("ov", keys.OldValue);
        Assert.Equal("m", keys.Modulus);
        Assert.Equal("e", keys.Exponent);
        Assert.Equal("om", keys.OldModulus);
        Assert.Equal("oe", keys.OldExponent);
    }

    [Fact]
    public async Task GetProofKeysAsync_NoProofKeyElement_ReturnsAllNullProperties()
    {
        var xml = XElement.Parse("<wopi-discovery />");
        var sut = CreateSut(xml);

        var keys = await sut.GetProofKeysAsync();

        Assert.Null(keys.Value);
        Assert.Null(keys.OldValue);
        Assert.Null(keys.Modulus);
        Assert.Null(keys.Exponent);
        Assert.Null(keys.OldModulus);
        Assert.Null(keys.OldExponent);
    }

    [Fact]
    public async Task GetProofKeysAsync_EmptyProofKeyElement_ReturnsAllNullProperties()
    {
        var xml = XElement.Parse(
            """
            <wopi-discovery>
                <proof-key />
            </wopi-discovery>
            """);
        var sut = CreateSut(xml);

        var keys = await sut.GetProofKeysAsync();

        Assert.Null(keys.Value);
        Assert.Null(keys.OldValue);
        Assert.Null(keys.Modulus);
        Assert.Null(keys.Exponent);
        Assert.Null(keys.OldModulus);
        Assert.Null(keys.OldExponent);
    }
}
