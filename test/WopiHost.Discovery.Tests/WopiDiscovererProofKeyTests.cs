using System.Xml.Linq;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace WopiHost.Discovery.Tests;

public class WopiDiscovererProofKeyTests
{
    private static WopiDiscoverer CreateSut(XElement discoveryXml)
    {
        var fileProvider = A.Fake<IDiscoveryFileProvider>();
        A.CallTo(() => fileProvider.GetDiscoveryXmlAsync()).Returns(Task.FromResult(discoveryXml));
        return new WopiDiscoverer(fileProvider, Options.Create(new DiscoveryOptions()), NullLogger<WopiDiscoverer>.Instance);
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
    public async Task GetProofKeysAsync_SpecificNetZone_StillResolvesRootProofKey()
    {
        // Proof-key lookup reads the discovery-document root and is deliberately NOT scoped by
        // NetZone (unlike the <app> lookup). The key here sits at the root, not inside <net-zone>,
        // so a net-zone-scoped lookup would miss it — pinning that a specific zone doesn't break
        // proof resolution on real OOS/M365 docs served from an external zone.
        var xml = XElement.Parse(
            """
            <wopi-discovery>
                <net-zone name="external-https">
                    <app name="Word">
                        <action ext="docx" name="edit" urlsrc="https://word.example/we/" />
                    </app>
                </net-zone>
                <proof-key value="v" oldvalue="ov" />
            </wopi-discovery>
            """);
        var fileProvider = A.Fake<IDiscoveryFileProvider>();
        A.CallTo(() => fileProvider.GetDiscoveryXmlAsync()).Returns(Task.FromResult(xml));
        var sut = new WopiDiscoverer(
            fileProvider,
            Options.Create(new DiscoveryOptions { NetZone = NetZoneEnum.ExternalHttps }),
            NullLogger<WopiDiscoverer>.Instance);

        var keys = await sut.GetProofKeysAsync();

        Assert.Equal("v", keys.Value);
        Assert.Equal("ov", keys.OldValue);
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

    [Fact]
    public async Task SupportsExtensionAsync_NetZoneWithoutName_FiltersOut()
    {
        // Discovery with a net-zone element missing the `name` attribute
        // should be skipped by ValidateNetZone, leaving no apps to inspect.
        var xml = XElement.Parse(
            """
            <wopi-discovery>
                <net-zone>
                    <app name="Excel">
                        <action ext="xlsx" name="edit" urlsrc="http://owaserver/x/" />
                    </app>
                </net-zone>
            </wopi-discovery>
            """);
        var sut = CreateSut(xml);

        var supported = await sut.SupportsExtensionAsync("xlsx");

        Assert.False(supported);
    }
}
