using System.IO;
using WopiHost.Discovery.Enumerations;
using Xunit;

namespace WopiHost.Discovery.Tests
{
	public class WopiDiscovererTests
	{
		private WopiDiscoverer _wopiDiscoverer;

		public WopiDiscovererTests()
		{
		    //TODO: test netzones	
		}

		public void InitDiscoverer(string fileName)
		{
			_wopiDiscoverer = new WopiDiscoverer(new FileSystemDiscoveryFileProvider(Path.Combine(System.AppContext.BaseDirectory, fileName)));
		}

		[Theory]
		[InlineData("xlsx", "OOS2016_discovery.xml")]
		[InlineData("docx", "OOS2016_discovery.xml")]
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
		[InlineData("html", "OOS2016_discovery.xml")]
		[InlineData("txt", "OOS2016_discovery.xml")]
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
		[InlineData("html", "OOS2016_discovery.xml")]
		[InlineData("txt", "OOS2016_discovery.xml")]
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
		[InlineData("pptx", "OOS2016_discovery.xml")]
		[InlineData("docx", "OOS2016_discovery.xml")]
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
		[InlineData("html", "OOS2016_discovery.xml")]
		[InlineData("txt", "OOS2016_discovery.xml")]
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
		[InlineData("docx", "OWA2013_discovery.xml", true)]
		[InlineData("docx", "OOS2016_discovery.xml", false)]
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
		[InlineData("xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>", "OWA2013_discovery.xml")]
		[InlineData("docx", WopiActionEnum.Edit, "http://owaserver/we/wordeditorframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>", "OWA2013_discovery.xml")]
		[InlineData("html", WopiActionEnum.Edit, null, "OWA2013_discovery.xml")]
		[InlineData("txt", WopiActionEnum.Edit, null, "OWA2013_discovery.xml")]
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
		[InlineData("xlsx", "Excel", "OOS2016_discovery.xml")]
		[InlineData("docx", "Word", "OOS2016_discovery.xml")]
		[InlineData("html", null, "OOS2016_discovery.xml")]
		[InlineData("txt", null, "OOS2016_discovery.xml")]
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
		[InlineData("xlsx", "http://owaserver/x/_layouts/resources/FavIcon_Excel.ico", "OOS2016_discovery.xml")]
		[InlineData("docx", "http://owaserver/wv/resources/1033/FavIcon_Word.ico", "OOS2016_discovery.xml")]
		[InlineData("html", null, "OOS2016_discovery.xml")]
		[InlineData("txt", null, "OOS2016_discovery.xml")]
		public async void FavIconTests(string extension, string expectedValue, string fileName)
		{
			// Arrange
			InitDiscoverer(fileName);

			// Act
			var result = await _wopiDiscoverer.GetApplicationFavIconAsync(extension);

			// Assert
			Assert.Equal(expectedValue, result);
		}
	}
}
