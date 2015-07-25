using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WopiFileSystemProvider.Tests
{
    [TestClass]
    public class WopiSecurityHandlerTests
    {
        [TestMethod]
        public void HashesMustNotMatch()
        {
            // Arrange
            WopiSecurityHandler securityHandler = new WopiSecurityHandler();

            // Act
            string token1 = securityHandler.GenerateAccessToken("test.docx");
            string token2 = securityHandler.GenerateAccessToken("test.docx");

            // Assert
            Assert.AreNotEqual(token2, token1, "Hashes must not match.");
        }

        [TestMethod]
        public void Generate_Simple_Hash_WithSalt()
        {
            // Arrange
            WopiSecurityHandler securityHandler = new WopiSecurityHandler();

            // Act
            string token = securityHandler.GenerateAccessToken("test.docx");
            bool result = securityHandler.ValidateAccessToken("test.docx", token);

            // Assert
            Assert.IsTrue(result, "Hash failed");
        }
    }
}
