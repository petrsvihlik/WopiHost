using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery.Tests;

public class WopiDiscovererTests
{
    private WopiDiscoverer? _wopiDiscoverer;
    private const string XmlOos2016 = "OOS2016_discovery.xml";
    private const string XmlOwa2013 = "OWA2013_discovery.xml";
    private const string XmlOo2019 = "OO2019_discovery.xml";
    private const string XmlInvalid = "INVALID_discovery.xml";

    public WopiDiscovererTests()
    {
    }

    [MemberNotNull(nameof(_wopiDiscoverer))]
    private void InitDiscoverer(string fileName, NetZoneEnum netZone) => 
        _wopiDiscoverer = new WopiDiscoverer(
            new FileSystemDiscoveryFileProvider(Path.Join(AppContext.BaseDirectory, fileName), NullLogger<FileSystemDiscoveryFileProvider>.Instance),
            Options.Create(new DiscoveryOptions { NetZone = netZone }),
            NullLogger<WopiDiscoverer>.Instance);

    [Theory]
    [InlineData(NetZoneEnum.ExternalHttps, "xlsm", WopiActionEnum.LegacyWebService, "https://excel.officeapps.live.com/x/_vti_bin/excelserviceinternal.asmx?<ui=UI_LLCC&><rs=DC_LLCC&><dchat=DISABLE_CHAT&><hid=HOST_SESSION_ID&><sc=SESSION_CONTEXT&><wopisrc=WOPI_SOURCE&>", XmlOo2019)]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.MobileView, "http://owaserver/x/_layouts/xlviewerinternal.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><dchat=DISABLE_CHAT&>", XmlOo2019)]
    [InlineData(NetZoneEnum.ExternalHttps, "xlsx", WopiActionEnum.MobileView, "https://excel.officeapps.live.com/x/_layouts/xlviewerinternal.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><dchat=DISABLE_CHAT&><hid=HOST_SESSION_ID&><sc=SESSION_CONTEXT&><wopisrc=WOPI_SOURCE&>", XmlOo2019)]
    [InlineData(NetZoneEnum.InternalHttp, "ods", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>", XmlOwa2013)]
    public async Task GetUrlTemplateAsync_NetZone_ReturnsTemplateForZone(NetZoneEnum netZone, string extension, WopiActionEnum action, string expectedValue, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.GetUrlTemplateAsync(extension, action);

        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public async Task GetAppsAsync_InvalidNetZone_ReturnsEmpty()
    {
        InitDiscoverer(XmlInvalid, NetZoneEnum.InternalHttp);

        var result = await _wopiDiscoverer.GetAppsAsync();

        Assert.Empty(result.Elements());
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", XmlOos2016)]
    public async Task SupportsExtensionAsync_KnownExtension_ReturnsTrue(NetZoneEnum netZone, string extension, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.SupportsExtensionAsync(extension);

        Assert.True(result, $"{extension} should be supported!");
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "XLSX", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "DOCX", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "XlSx", XmlOos2016)]
    public async Task SupportsExtensionAsync_KnownExtensionCaseInsensitive_ReturnsTrue(NetZoneEnum netZone, string extension, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.SupportsExtensionAsync(extension);

        Assert.True(result, $"{extension} should be supported!");
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "html", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "txt", XmlOos2016)]
    public async Task SupportsExtensionAsync_UnknownExtension_ReturnsFalse(NetZoneEnum netZone, string extension, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.SupportsExtensionAsync(extension);

        Assert.False(result, $"{extension} should not be supported!");
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "html", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "txt", XmlOos2016)]
    public async Task SupportsActionAsync_UnknownExtension_ReturnsFalse(NetZoneEnum netZone, string extension, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.SupportsActionAsync(extension, WopiActionEnum.Edit);

        Assert.False(result, $"{extension} should not be supported!");
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "pptx", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", XmlOos2016)]
    public async Task SupportsActionAsync_KnownExtension_ReturnsTrue(NetZoneEnum netZone, string extension, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.SupportsActionAsync(extension, WopiActionEnum.Edit);

        Assert.True(result, $"{extension} should be supported!");
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "PPTX", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "DOCX", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "PpTx", XmlOos2016)]
    public async Task SupportsActionAsync_KnownExtensionCaseInsensitive_ReturnsTrue(NetZoneEnum netZone, string extension, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.SupportsActionAsync(extension, WopiActionEnum.Edit);

        Assert.True(result, $"{extension} should be supported!");
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "html", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "txt", XmlOos2016)]
    public async Task RequiresCobaltAsync_UnknownExtension_ReturnsFalse(NetZoneEnum netZone, string extension, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.RequiresCobaltAsync(extension, WopiActionEnum.Edit);

        Assert.False(result, $"{extension} should not be supported!");
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "docx", XmlOwa2013, true)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", XmlOos2016, false)]
    public async Task RequiresCobaltAsync_KnownExtension_ReturnsExpected(NetZoneEnum netZone, string extension, string fileName, bool expected)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.RequiresCobaltAsync(extension, WopiActionEnum.Edit);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", WopiActionEnum.Edit, "http://owaserver/we/wordeditorframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "html", WopiActionEnum.Edit, null, XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "txt", WopiActionEnum.Edit, null, XmlOwa2013)]
    public async Task GetUrlTemplateAsync_ExtensionAndAction_ReturnsTemplate(NetZoneEnum netZone, string extension, WopiActionEnum action, string? expectedValue, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.GetUrlTemplateAsync(extension, action);

        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "XLSX", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "DOCX", WopiActionEnum.Edit, "http://owaserver/we/wordeditorframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "XlSx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>", XmlOwa2013)]
    public async Task GetUrlTemplateAsync_ExtensionCaseInsensitive_ReturnsTemplate(NetZoneEnum netZone, string extension, WopiActionEnum action, string? expectedValue, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.GetUrlTemplateAsync(extension, action);

        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", "Excel", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", "Word", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "html", null, XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "txt", null, XmlOos2016)]
    public async Task GetApplicationNameAsync_Extension_ReturnsAppName(NetZoneEnum netZone, string extension, string? expectedValue, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.GetApplicationNameAsync(extension);

        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "XLSX", "Excel", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "DOCX", "Word", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "XlSx", "Excel", XmlOos2016)]
    public async Task GetApplicationNameAsync_ExtensionCaseInsensitive_ReturnsAppName(NetZoneEnum netZone, string extension, string? expectedValue, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.GetApplicationNameAsync(extension);

        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", "http://owaserver/x/_layouts/resources/FavIcon_Excel.ico", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", "http://owaserver/wv/resources/1033/FavIcon_Word.ico", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "html", null, XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "txt", null, XmlOos2016)]
    public async Task GetApplicationFavIconAsync_Extension_ReturnsFavIconUri(NetZoneEnum netZone, string extension, string? expectedValue, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.GetApplicationFavIconAsync(extension);

        Assert.Equal(expectedValue is not null ? new Uri(expectedValue) : null, result);
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.Edit, "update", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", WopiActionEnum.Edit, "locks", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", WopiActionEnum.Edit, "cobalt", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", WopiActionEnum.Edit, "update", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "one", WopiActionEnum.View, "containers", XmlOwa2013)]
    public async Task GetActionRequirementsAsync_ExtensionAndAction_ContainsRequirement(NetZoneEnum netZone, string extension, WopiActionEnum action, string expectedValue, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.GetActionRequirementsAsync(extension, action);

        Assert.Contains(expectedValue, result);
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "one", WopiActionEnum.Edit, "locks", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.Edit, "locks", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.Edit, "cobalt", XmlOos2016)]
    public async Task GetActionRequirementsAsync_ExtensionAndAction_DoesNotContainUnrelatedRequirement(NetZoneEnum netZone, string extension, WopiActionEnum action, string expectedValue, string fileName)
    {
        InitDiscoverer(fileName, netZone);

        var result = await _wopiDiscoverer.GetActionRequirementsAsync(extension, action);

        Assert.DoesNotContain(expectedValue, result);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        // Two AsyncExpiringLazy instances and one IDisposable proof-key cache live inside the
        // discoverer. Double-Dispose must short-circuit on the second call so the inner
        // resources aren't double-disposed.
        InitDiscoverer(XmlOo2019, NetZoneEnum.ExternalHttps);

        _wopiDiscoverer.Dispose();
        _wopiDiscoverer.Dispose();
    }

    [Fact]
    public async Task GetProofKeysAsync_RealCspBlob_ImportsIntoRsaProvider()
    {
        // The checked-in OWA2013 discovery XML carries Microsoft's real proof-key CSP blobs in its
        // root <proof-key value=.. oldvalue=..>. WopiProofValidator feeds exactly this base64
        // through RSACryptoServiceProvider.ImportCspBlob, so the fixture's bytes must round-trip
        // into a usable key on the runtime that ships the host.
        InitDiscoverer(XmlOwa2013, NetZoneEnum.InternalHttp);

        var keys = await _wopiDiscoverer.GetProofKeysAsync();

        Assert.False(string.IsNullOrEmpty(keys.Value));
        Assert.False(string.IsNullOrEmpty(keys.OldValue));

        using var rsa = new RSACryptoServiceProvider();
        rsa.ImportCspBlob(Convert.FromBase64String(keys.Value!));
        Assert.True(rsa.KeySize > 0);

        using var rsaOld = new RSACryptoServiceProvider();
        rsaOld.ImportCspBlob(Convert.FromBase64String(keys.OldValue!));
        Assert.True(rsaOld.KeySize > 0);
    }
}
