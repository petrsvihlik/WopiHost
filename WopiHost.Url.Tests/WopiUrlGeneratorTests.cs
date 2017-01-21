using System.Globalization;
using System.IO;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using Xunit;

namespace WopiHost.Url.Tests
{
	public class WopiUrlGeneratorTests
	{
		private readonly IDiscoveryFileProvider _fileProvider;

		public WopiUrlGeneratorTests()
		{
			_fileProvider = new FileSystemDiscoveryFileProvider(Path.Combine(System.AppContext.BaseDirectory, "OOS2016_discovery.xml"));
		}

		[Theory]
		[InlineData("xlsx", "http://wopihost:5000/wopi/files/test.xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&WOPISrc=http%3A%2F%2Fwopihost%3A5000%2Fwopi%2Ffiles%2Ftest.xlsx")]
		[InlineData("docx", "http://wopihost:5000/wopi/files/test.docx", WopiActionEnum.View, "http://owaserver/wv/wordviewerframe.aspx?&WOPISrc=http%3A%2F%2Fwopihost%3A5000%2Fwopi%2Ffiles%2Ftest.docx")]
		public async void UrlWithoutAdditionalSettings(string extension, string wopiFileUrl, WopiActionEnum action, string expectedValue)
		{
			// Arrange
			var urlGenerator = new WopiUrlGenerator(_fileProvider);

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
			var settings = new WopiUrlSettings { UI_LLCC = new CultureInfo("en-US") };
			var urlGenerator = new WopiUrlGenerator(_fileProvider, settings);

			// Act
			var result = await urlGenerator.GetFileUrlAsync(extension, wopiFileUrl, action);

			// Assert
			Assert.Equal(expectedValue, result);
		}
	}
}
