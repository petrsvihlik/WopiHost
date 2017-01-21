using System.IO;
using WopiHost.Discovery.Enumerations;
using Xunit;

namespace WopiHost.Discovery.Tests
{
	public class WopiDiscovererTests
	{
		private readonly WopiDiscoverer _wopiDiscoverer;

		public WopiDiscovererTests()
		{
			_wopiDiscoverer = new WopiDiscoverer(new FileSystemDiscoveryFileProvider(Path.Combine(System.AppContext.BaseDirectory, "OOS2016_discovery.xml")));
		}

		[Theory]
		[InlineData("xlsx")]
		[InlineData("docx")]
		public async void SupportedExtension(string extension)
		{
			// Act
			var result = await _wopiDiscoverer.SupportsExtensionAsync(extension);

			// Assert
			Assert.True(result, $"{extension} should be supported!");
		}

		[Theory]
		[InlineData("html")]
		[InlineData("txt")]
		public async void NonSupportedExtension(string extension)
		{
			// Act
			var result = await _wopiDiscoverer.SupportsExtensionAsync(extension);

			// Assert
			Assert.False(result, $"{extension} should not be supported!");
		}

		[Theory]
		[InlineData("html")]
		[InlineData("txt")]
		public async void NonSupportedExtensionWithAction(string extension)
		{
			// Act
			var result = await _wopiDiscoverer.SupportsActionAsync(extension, WopiActionEnum.Edit);

			// Assert
			Assert.False(result, $"{extension} should not be supported!");
		}

		[Theory]
		[InlineData("pptx")]
		[InlineData("docx")]
		public async void SupportedExtensionWithAction(string extension)
		{
			// Act
			var result = await _wopiDiscoverer.SupportsActionAsync(extension, WopiActionEnum.Edit);

			// Assert
			Assert.True(result, $"{extension} should be supported!");
		}

		[Theory]
		[InlineData("html")]
		[InlineData("txt")]
		public async void NonSupportedExtensionCobalt(string extension)
		{
			// Act
			var result = await _wopiDiscoverer.RequiresCobaltAsync(extension, WopiActionEnum.Edit);

			// Assert
			Assert.False(result, $"{extension} should not be supported!");
		}

		[Theory]
		[InlineData("docx")]
		public async void SupportedExtensionCobalt(string extension)
		{
			// Act
			var result = await _wopiDiscoverer.RequiresCobaltAsync(extension, WopiActionEnum.Edit);

			// Assert
			Assert.True(result, $"{extension} should be required!");
		}

		[Theory]
		[InlineData("xlsx", WopiActionEnum.Edit, "http://owaserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>")]
		[InlineData("docx", WopiActionEnum.Edit, "http://owaserver/we/wordeditorframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>")]
		[InlineData("html", WopiActionEnum.Edit, null)]
		[InlineData("txt", WopiActionEnum.Edit, null)]
		public async void UrlTemplateTests(string extension, WopiActionEnum action, string expectedValue)
		{
			// Act
			var result = await _wopiDiscoverer.GetUrlTemplateAsync(extension, action);

			// Assert
			Assert.Equal(expectedValue, result);
		}

		[Theory]
		[InlineData("xlsx", "Excel")]
		[InlineData("docx", "Word")]
		[InlineData("html", null)]
		[InlineData("txt", null)]
		public async void AppNameTests(string extension, string expectedValue)
		{
			// Act
			var result = await _wopiDiscoverer.GetApplicationNameAsync(extension);

			// Assert
			Assert.Equal(expectedValue, result);
		}

		[Theory]
		[InlineData("xlsx", "http://owaserver/x/_layouts/images/FavIcon_Excel.ico")]
		[InlineData("docx", "http://owaserver/wv/resources/1033/FavIcon_Word.ico")]
		[InlineData("html", null)]
		[InlineData("txt", null)]
		public async void FavIconTests(string extension, string expectedValue)
		{
			// Act
			var result = await _wopiDiscoverer.GetApplicationFavIconAsync(extension);

			// Assert
			Assert.Equal(expectedValue, result);
		}
	}
}
