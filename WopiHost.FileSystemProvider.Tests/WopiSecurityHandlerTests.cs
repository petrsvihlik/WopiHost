using Xunit;

namespace WopiHost.FileSystemProvider.Tests
{
	public class WopiSecurityHandlerTests
	{
		[Fact]
		public void HashesMustNotMatch()
		{
			// Arrange
			WopiSecurityHandler securityHandler = new WopiSecurityHandler();

			// Act
			string token1 = securityHandler.GenerateAccessToken("test.docx");
			string token2 = securityHandler.GenerateAccessToken("test.docx");

			// Assert
			Assert.NotEqual(token2, token1);
		}

		[Fact]
		public void Generate_Simple_Hash_WithSalt()
		{
			// Arrange
			WopiSecurityHandler securityHandler = new WopiSecurityHandler();

			// Act
			string token = securityHandler.GenerateAccessToken("test.docx");
			bool result = securityHandler.ValidateAccessToken("test.docx", token);

			// Assert
			Assert.True(result);
		}
	}
}
