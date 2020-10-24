using System;
using System.IO;
using WopiHost.Discovery.Enumerations;
using Xunit;

namespace WopiHost.Discovery.Tests
{
    public class WopiDiscovererTests
    {
        private WopiDiscoverer _wopiDiscoverer;
        private const string XmlOos2016 = "OOS2016_discovery.xml";
        private const string XmlOwa2013 = "OWA2013_discovery.xml";
        private const string XmlOo2019 = "OO2019_discovery.xml";

        public WopiDiscovererTests()
        {
            //TODO: test netzones	
        }

        private void InitDiscoverer(string fileName, NetZoneEnum netZone = NetZoneEnum.Any)
        {
            _wopiDiscoverer = new WopiDiscoverer(new FileSystemDiscoveryFileProvider(Path.Combine(System.AppContext.BaseDirectory, fileName)), netZone);
        }

        [Theory]
        [InlineData(NetZoneEnum.ExternalHttps, "xlsm", WopiActionEnum.LegacyWebService, "https://excel.officeapps.live.com/x/_vti_bin/excelserviceinternal.asmx?<ui=UI_LLCC&><rs=DC_LLCC&><dchat=DISABLE_CHAT&><hid=HOST_SESSION_ID&><sc=SESSION_CONTEXT&><wopisrc=WOPI_SOURCE&>", XmlOo2019)]
        [InlineData(NetZoneEnum.InternalHttp, "xlsx", WopiActionEnum.MobileView, "http://owaserver/x/_layouts/xlviewerinternal.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><dchat=DISABLE_CHAT&>", XmlOo2019)]
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

        [Theory]
        [InlineData("xlsx", XmlOos2016)]
        [InlineData("docx", XmlOos2016)]
        public async void SupportedExtension(string extension, string fileName)
        {
            // Arrange
            InitDiscoverer(fileName);

            // Act
            var result = await _wopiDiscoverer.SupportsExtensionAsync(extension);

            // Assert
            Assert.True(result, $"{extension} should be supported!");
        }

        [Theory]
        [InlineData("html", XmlOos2016)]
        [InlineData("txt", XmlOos2016)]
        public async void NonSupportedExtension(string extension, string fileName)
        {
            // Arrange
            InitDiscoverer(fileName);

            // Act
            var result = await _wopiDiscoverer.SupportsExtensionAsync(extension);

            // Assert
            Assert.False(result, $"{extension} should not be supported!");
        }

        [Theory]
        [InlineData("html", XmlOos2016)]
        [InlineData("txt", XmlOos2016)]
        public async void NonSupportedExtensionWithAction(string extension, string fileName)
        {
            // Arrange
            InitDiscoverer(fileName);

            // Act
            var result = await _wopiDiscoverer.SupportsActionAsync(extension, WopiActionEnum.Edit);

            // Assert
            Assert.False(result, $"{extension} should not be supported!");
        }

        [Theory]
        [InlineData("pptx", XmlOos2016)]
        [InlineData("docx", XmlOos2016)]
        public async void SupportedExtensionWithAction(string extension, string fileName)
        {
            // Arrange
            InitDiscoverer(fileName);

            // Act
            var result = await _wopiDiscoverer.SupportsActionAsync(extension, WopiActionEnum.Edit);

            // Assert
            Assert.True(result, $"{extension} should be supported!");
        }

        [Theory]
        [InlineData("html", XmlOos2016)]
        [InlineData("txt", XmlOos2016)]
        public async void NonSupportedExtensionCobalt(string extension, string fileName)
        {
            // Arrange
            InitDiscoverer(fileName);

            // Act
            var result = await _wopiDiscoverer.RequiresCobaltAsync(extension, WopiActionEnum.Edit);

            // Assert
            Assert.False(result, $"{extension} should not be supported!");
        }

        [Theory]
        [InlineData("docx", XmlOwa2013, true)]
        [InlineData("docx", XmlOos2016, false)]
        public async void SupportedExtensionCobalt(string extension, string fileName, bool expected)
        {
            // Arrange
            InitDiscoverer(fileName);

            // Act
            var result = await _wopiDiscoverer.RequiresCobaltAsync(extension, WopiActionEnum.Edit);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>", XmlOwa2013)]
        [InlineData("docx", WopiActionEnum.Edit, "http://owaserver/we/wordeditorframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>", XmlOwa2013)]
        [InlineData("html", WopiActionEnum.Edit, null, XmlOwa2013)]
        [InlineData("txt", WopiActionEnum.Edit, null, XmlOwa2013)]
        public async void UrlTemplateTests(string extension, WopiActionEnum action, string expectedValue, string fileName)
        {
            // Arrange
            InitDiscoverer(fileName);

            // Act
            var result = await _wopiDiscoverer.GetUrlTemplateAsync(extension, action);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData("xlsx", "Excel", XmlOos2016)]
        [InlineData("docx", "Word", XmlOos2016)]
        [InlineData("html", null, XmlOos2016)]
        [InlineData("txt", null, XmlOos2016)]
        public async void AppNameTests(string extension, string expectedValue, string fileName)
        {
            // Arrange
            InitDiscoverer(fileName);

            // Act
            var result = await _wopiDiscoverer.GetApplicationNameAsync(extension);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData("xlsx", "http://owaserver/x/_layouts/resources/FavIcon_Excel.ico", XmlOos2016)]
        [InlineData("docx", "http://owaserver/wv/resources/1033/FavIcon_Word.ico", XmlOos2016)]
        [InlineData("html", null, XmlOos2016)]
        [InlineData("txt", null, XmlOos2016)]
        public async void FavIconTests(string extension, string expectedValue, string fileName)
        {
            // Arrange
            InitDiscoverer(fileName);

            // Act
            var result = await _wopiDiscoverer.GetApplicationFavIconAsync(extension);

            // Assert
            Assert.Equal(expectedValue != null ? new Uri(expectedValue) : null, result);
        }

        [Theory]
        [InlineData("xlsx", WopiActionEnum.Edit, "update", XmlOwa2013)]
        [InlineData("docx", WopiActionEnum.Edit, "locks", XmlOwa2013)]
        [InlineData("docx", WopiActionEnum.Edit, "cobalt", XmlOwa2013)]
        [InlineData("docx", WopiActionEnum.Edit, "update", XmlOos2016)]
        [InlineData("one", WopiActionEnum.View, "containers", XmlOwa2013)]
        public async void ActionRequirementsTests(string extension, WopiActionEnum action, string expectedValue, string fileName)
        {
            // Arrange
            InitDiscoverer(fileName);

            // Act
            var result = await _wopiDiscoverer.GetActionRequirementsAsync(extension, action);

            // Assert
            Assert.Contains(expectedValue, result);
        }

        [Theory]
        [InlineData("xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>", XmlOwa2013)]
        [InlineData("one", WopiActionEnum.Edit, "locks", XmlOwa2013)]
        [InlineData("xlsx", WopiActionEnum.Edit, "locks", XmlOwa2013)]
        [InlineData("xlsx", WopiActionEnum.Edit, "cobalt", XmlOos2016)]
        public async void ActionRequirementsNegativeTests(string extension, WopiActionEnum action, string expectedValue, string fileName)
        {
            // Arrange
            InitDiscoverer(fileName);

            // Act
            var result = await _wopiDiscoverer.GetActionRequirementsAsync(extension, action);

            // Assert
            Assert.DoesNotContain(expectedValue, result);
        }
    }
}
