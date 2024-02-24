using System.Xml.Linq;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery.Tests;

public class WopiDiscovererTests
{
    private WopiDiscoverer _wopiDiscoverer;
    private const string XmlOos2016 = "OOS2016_discovery.xml";
    private const string XmlOwa2013 = "OWA2013_discovery.xml";
    private const string XmlOo2019 = "OO2019_discovery.xml";
    private const string XmlInvalid = "INVALID_discovery.xml";

    public WopiDiscovererTests()
    {
    }

    private void InitDiscoverer(string fileName, NetZoneEnum netZone) => _wopiDiscoverer = new WopiDiscoverer(new FileSystemDiscoveryFileProvider(Path.Combine(AppContext.BaseDirectory, fileName)), new DiscoveryOptions { NetZone = netZone });

    [Theory]
    [InlineData(NetZoneEnum.ExternalHttps, "xlsm", WopiActionEnum.LegacyWebService, "https://excel.officeapps.live.com/x/_vti_bin/excelserviceinternal.asmx?<ui=UI_LLCC&><rs=DC_LLCC&><dchat=DISABLE_CHAT&><hid=HOST_SESSION_ID&><sc=SESSION_CONTEXT&><wopisrc=WOPI_SOURCE&>", XmlOo2019)]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.MobileView, "http://owaserver/x/_layouts/xlviewerinternal.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><dchat=DISABLE_CHAT&>", XmlOo2019)]
    [InlineData(NetZoneEnum.ExternalHttps, "xlsx", WopiActionEnum.MobileView, "https://excel.officeapps.live.com/x/_layouts/xlviewerinternal.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><dchat=DISABLE_CHAT&><hid=HOST_SESSION_ID&><sc=SESSION_CONTEXT&><wopisrc=WOPI_SOURCE&>", XmlOo2019)]
    [InlineData(NetZoneEnum.InternalHttp, "ods", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>", XmlOwa2013)]
    public async void NetZoneTests(NetZoneEnum netZone, string extension, WopiActionEnum action, string expectedValue, string fileName)
    {
        // Arrange
        InitDiscoverer(fileName, netZone);

        // Act
        var result = await _wopiDiscoverer.GetUrlTemplateAsync(extension, action);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public async void InvalidNetZone()
    {
        // Arrange
        InitDiscoverer(XmlInvalid, NetZoneEnum.InternalHttp);

        // Act
        var result = await _wopiDiscoverer.GetAppsAsync();

        // Assert
        Assert.Empty(result.Elements());
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", XmlOos2016)]
    public async void SupportedExtension(NetZoneEnum netZone, string extension, string fileName)
    {
        // Arrange
        InitDiscoverer(fileName, netZone);

        // Act
        var result = await _wopiDiscoverer.SupportsExtensionAsync(extension);

        // Assert
        Assert.True(result, $"{extension} should be supported!");
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "html", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "txt", XmlOos2016)]
    public async void NonSupportedExtension(NetZoneEnum netZone, string extension, string fileName)
    {
        // Arrange
        InitDiscoverer(fileName, netZone);

        // Act
        var result = await _wopiDiscoverer.SupportsExtensionAsync(extension);

        // Assert
        Assert.False(result, $"{extension} should not be supported!");
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "html", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "txt", XmlOos2016)]
    public async void NonSupportedExtensionWithAction(NetZoneEnum netZone, string extension, string fileName)
    {
        // Arrange
        InitDiscoverer(fileName, netZone);

        // Act
        var result = await _wopiDiscoverer.SupportsActionAsync(extension, WopiActionEnum.Edit);

        // Assert
        Assert.False(result, $"{extension} should not be supported!");
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "pptx", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", XmlOos2016)]
    public async void SupportedExtensionWithAction(NetZoneEnum netZone, string extension, string fileName)
    {
        // Arrange
        InitDiscoverer(fileName, netZone);

        // Act
        var result = await _wopiDiscoverer.SupportsActionAsync(extension, WopiActionEnum.Edit);

        // Assert
        Assert.True(result, $"{extension} should be supported!");
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "html", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "txt", XmlOos2016)]
    public async void NonSupportedExtensionCobalt(NetZoneEnum netZone, string extension, string fileName)
    {
        // Arrange
        InitDiscoverer(fileName, netZone);

        // Act
        var result = await _wopiDiscoverer.RequiresCobaltAsync(extension, WopiActionEnum.Edit);

        // Assert
        Assert.False(result, $"{extension} should not be supported!");
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "docx", XmlOwa2013, true)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", XmlOos2016, false)]
    public async void SupportedExtensionCobalt(NetZoneEnum netZone, string extension, string fileName, bool expected)
    {
        // Arrange
        InitDiscoverer(fileName, netZone);

        // Act
        var result = await _wopiDiscoverer.RequiresCobaltAsync(extension, WopiActionEnum.Edit);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", WopiActionEnum.Edit, "http://owaserver/we/wordeditorframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "html", WopiActionEnum.Edit, null, XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "txt", WopiActionEnum.Edit, null, XmlOwa2013)]
    public async void UrlTemplateTests(NetZoneEnum netZone, string extension, WopiActionEnum action, string? expectedValue, string fileName)
    {
        // Arrange
        InitDiscoverer(fileName, netZone);

        // Act
        var result = await _wopiDiscoverer.GetUrlTemplateAsync(extension, action);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", "Excel", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", "Word", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "html", null, XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "txt", null, XmlOos2016)]
    public async void AppNameTests(NetZoneEnum netZone, string extension, string? expectedValue, string fileName)
    {
        // Arrange
        InitDiscoverer(fileName, netZone);

        // Act
        var result = await _wopiDiscoverer.GetApplicationNameAsync(extension);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", "http://owaserver/x/_layouts/resources/FavIcon_Excel.ico", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", "http://owaserver/wv/resources/1033/FavIcon_Word.ico", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "html", null, XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "txt", null, XmlOos2016)]
    public async void FavIconTests(NetZoneEnum netZone, string extension, string? expectedValue, string fileName)
    {
        // Arrange
        InitDiscoverer(fileName, netZone);

        // Act
        var result = await _wopiDiscoverer.GetApplicationFavIconAsync(extension);

        // Assert
        Assert.Equal(expectedValue is not null ? new Uri(expectedValue) : null, result);
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.Edit, "update", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", WopiActionEnum.Edit, "locks", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", WopiActionEnum.Edit, "cobalt", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "docx", WopiActionEnum.Edit, "update", XmlOos2016)]
    [InlineData(NetZoneEnum.InternalHttp, "one", WopiActionEnum.View, "containers", XmlOwa2013)]
    public async void ActionRequirementsTests(NetZoneEnum netZone, string extension, WopiActionEnum action, string expectedValue, string fileName)
    {
        // Arrange
        InitDiscoverer(fileName, netZone);

        // Act
        var result = await _wopiDiscoverer.GetActionRequirementsAsync(extension, action);

        // Assert
        Assert.Contains(expectedValue, result);
    }

    [Theory]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "one", WopiActionEnum.Edit, "locks", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.Edit, "locks", XmlOwa2013)]
    [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.Edit, "cobalt", XmlOos2016)]
    public async void ActionRequirementsNegativeTests(NetZoneEnum netZone, string extension, WopiActionEnum action, string expectedValue, string fileName)
    {
        // Arrange
        InitDiscoverer(fileName, netZone);

        // Act
        var result = await _wopiDiscoverer.GetActionRequirementsAsync(extension, action);

        // Assert
        Assert.DoesNotContain(expectedValue, result);
    }
}
