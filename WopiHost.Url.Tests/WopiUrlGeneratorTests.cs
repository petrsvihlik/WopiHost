using System;
using System.Globalization;
using FakeItEasy;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using Xunit;

namespace WopiHost.Url.Tests
{
	public class WopiUrlGeneratorTests
	{
		private readonly IDiscoverer _discoverer;

		public WopiUrlGeneratorTests()
		{
			_discoverer = A.Fake<IDiscoverer>();
			A.CallTo(() => _discoverer.GetUrlTemplateAsync("xlsx", WopiActionEnum.Edit)).ReturnsLazily(() => "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>");
			A.CallTo(() => _discoverer.GetUrlTemplateAsync("docx", WopiActionEnum.View)).ReturnsLazily(() => "http://owaserver/wv/wordviewerframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>");
		}

		[Theory]
		[InlineData("xlsx", "http://wopihost:5000/wopi/files/test.xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&WOPISrc=http%3A%2F%2Fwopihost%3A5000%2Fwopi%2Ffiles%2Ftest.xlsx")]
		[InlineData("docx", "http://wopihost:5000/wopi/files/test.docx", WopiActionEnum.View, "http://owaserver/wv/wordviewerframe.aspx?&WOPISrc=http%3A%2F%2Fwopihost%3A5000%2Fwopi%2Ffiles%2Ftest.docx")]
		public async void UrlWithoutAdditionalSettings(string extension, string wopiFileUrl, WopiActionEnum action, string expectedValue)
		{
			// Arrange
			var urlGenerator = new WopiUrlBuilder(_discoverer);

			// Act
			var result = await urlGenerator.GetFileUrlAsync(extension, wopiFileUrl, action);

			// Assert
			Assert.Equal(expectedValue, result);
		}

		[Theory]
		[InlineData("xlsx", "http://wopihost:5000/wopi/files/test.xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&ui=en-US&WOPISrc=http%3A%2F%2Fwopihost%3A5000%2Fwopi%2Ffiles%2Ftest.xlsx")]
		[InlineData("docx", "http://wopihost:5000/wopi/files/test.docx", WopiActionEnum.View, "http://owaserver/wv/wordviewerframe.aspx?ui=en-US&WOPISrc=http%3A%2F%2Fwopihost%3A5000%2Fwopi%2Ffiles%2Ftest.docx")]
		public async void UrlWithAdditionalSettings(string extension, string wopiFileUrl, WopiActionEnum action, string expectedValue)
		{
			// Arrange
			var settings = new WopiUrlSettings { UiLlcc = new CultureInfo("en-US") };
			var urlGenerator = new WopiUrlBuilder(_discoverer, settings);

			// Act
			var result = await urlGenerator.GetFileUrlAsync(extension, wopiFileUrl, action);

			// Assert
			Assert.Equal(expectedValue, result);
		}


		[Theory]
		[InlineData("html", "http://wopihost:5000/wopi/files/test.xlsx", WopiActionEnum.Edit, null)]
		public async void NonExistentTemplate(string extension, string wopiFileUrl, WopiActionEnum action, string expectedValue)
		{
			// Arrange
			var urlGenerator = new WopiUrlBuilder(_discoverer);

			// Act
			var result = await urlGenerator.GetFileUrlAsync(extension, wopiFileUrl, action);

			// Assert
			Assert.Equal(expectedValue, result);
		}

        [Fact]
        public void SettingsArePresent()
        {
            // Arrange
            var settings = new WopiUrlSettings()
            {
                BusinessUser = 1,
                UiLlcc = new CultureInfo("en-US"),
                DcLlcc = new CultureInfo("es-ES"),
                Embedded = true,
                DisableAsync = true,
                DisableBroadcast = true,
                Fullscreen = true,
                Recording = true,
                ThemeId = 1,
                DisableChat = 1,
                Perfstats = 1,
                HostSessionId = Guid.NewGuid().ToString(),
                SessionContext = Guid.NewGuid().ToString(),
                WopiSource = "c:\\doc.docx",
                ValidatorTestCategory = "All"
            };

            // Act

            // Assert
            Assert.Equal(15, settings.Count);
        }
    }
}
