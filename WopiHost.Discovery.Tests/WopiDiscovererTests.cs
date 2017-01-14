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
	}
}
