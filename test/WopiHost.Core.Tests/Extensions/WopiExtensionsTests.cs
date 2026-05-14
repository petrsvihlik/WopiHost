using System.Security.Cryptography;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Tests.Extensions;

public class WopiExtensionsTests
{
    [Fact]
    public async Task GetEncodedSha256_ReturnsBase64Checksum_WhenChecksumIsProvided()
    {
        var mockFile = new Mock<IWopiFile>();
        var checksum = new byte[] { 1, 2, 3, 4, 5 };
        mockFile.Setup(f => f.Checksum).Returns(new ReadOnlyMemory<byte>(checksum));

        var result = await mockFile.Object.GetEncodedSha256();

        Assert.Equal(Convert.ToBase64String(checksum), result);
    }

    [Fact]
    public async Task GetEncodedSha256_CalculatesChecksum_WhenChecksumIsNotProvided()
    {
        var mockFile = new Mock<IWopiFile>();
        mockFile.Setup(f => f.Checksum).Returns((ReadOnlyMemory<byte>?)null);
        var stream = new MemoryStream([1, 2, 3, 4, 5]);
        mockFile.Setup(f => f.OpenReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(stream);

        var result = await mockFile.Object.GetEncodedSha256();

        var expectedChecksum = SHA256.HashData([1, 2, 3, 4, 5]);
        Assert.Equal(Convert.ToBase64String(expectedChecksum), result);
    }
}
