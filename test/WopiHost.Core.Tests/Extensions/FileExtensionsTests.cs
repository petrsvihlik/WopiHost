using System.Security.Cryptography;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Tests.Extensions;

public class FileExtensionsTests
{
    [Fact]
    public async Task GetEncodedSha256_ReturnsBase64Checksum_WhenChecksumIsProvided()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        var checksum = new byte[] { 1, 2, 3, 4, 5 };
        mockFile.Setup(f => f.Checksum).Returns(checksum);

        // Act
        var result = await mockFile.Object.GetEncodedSha256();

        // Assert
        Assert.Equal(Convert.ToBase64String(checksum), result);
    }

    [Fact]
    public async Task GetEncodedSha256_CalculatesChecksum_WhenChecksumIsNotProvided()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Checksum).Returns((byte[]?)null);
        var stream = new MemoryStream([1, 2, 3, 4, 5]);
        mockFile.Setup(f => f.GetReadStream(It.IsAny<CancellationToken>())).ReturnsAsync(stream);

        // Act
        var result = await mockFile.Object.GetEncodedSha256();

        // Assert
        var stream2 = new MemoryStream([1, 2, 3, 4, 5]);
        var expectedChecksum = await SHA256.Create().ComputeHashAsync(stream2);
        Assert.Equal(Convert.ToBase64String(expectedChecksum), result);
    }

    [Fact]
    public void GetWopiCheckFileInfo_ReturnsCorrectInfo()
    {
        // Arrange
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Name).Returns("test");
        mockFile.Setup(f => f.Owner).Returns("owner");
        mockFile.Setup(f => f.Extension).Returns("txt");
        mockFile.Setup(f => f.LastWriteTimeUtc).Returns(DateTime.UtcNow);
        mockFile.Setup(f => f.Exists).Returns(true);
        mockFile.Setup(f => f.Length).Returns(12345);

        var capabilities = new WopiHostCapabilities
        {
            SupportsCoauth = true,
            SupportsFolders = true,
            SupportsLocks = true,
            SupportsGetLock = true,
            SupportsExtendedLockLength = true,
            SupportsEcosystem = true,
            SupportsGetFileWopiSrc = true,
            SupportedShareUrlTypes = new[] { "ReadOnly", "ReadWrite" },
            SupportsScenarioLinks = true,
            SupportsSecureStore = true,
            SupportsUpdate = true,
            SupportsCobalt = true,
            SupportsRename = true,
            SupportsDeleteFile = true,
            SupportsUserInfo = true,
            SupportsFileCreation = true
        };

        // Act
        var result = mockFile.Object.GetWopiCheckFileInfo(capabilities);

        // Assert
        Assert.Equal("owner", result.OwnerId);
        Assert.Equal("test.txt", result.BaseFileName);
        Assert.Equal(".txt", result.FileExtension);
        Assert.Equal(12345, result.Size);
        Assert.True(result.SupportsCoauth);
        Assert.True(result.SupportsFolders);
        Assert.True(result.SupportsLocks);
        Assert.True(result.SupportsGetLock);
        Assert.True(result.SupportsExtendedLockLength);
        Assert.True(result.SupportsEcosystem);
        Assert.True(result.SupportsGetFileWopiSrc);
        Assert.Equal(["ReadOnly", "ReadWrite"], result.SupportedShareUrlTypes);
        Assert.True(result.SupportsScenarioLinks);
        Assert.True(result.SupportsSecureStore);
        Assert.True(result.SupportsUpdate);
        Assert.True(result.SupportsCobalt);
        Assert.True(result.SupportsRename);
        Assert.True(result.SupportsDeleteFile);
        Assert.True(result.SupportsUserInfo);
        Assert.True(result.SupportsFileCreation);
    }
}
