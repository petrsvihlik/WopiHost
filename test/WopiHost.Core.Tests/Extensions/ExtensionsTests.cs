using System.Text;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Tests.Extensions;

public class ExtensionsTests
{
    [Fact]
    public void ToUnixTimestamp_DateTime_ReturnsUnixSeconds()
    {
        long ticks = 1664582400;
        DateTime dateTime = new(2022, 10, 1);

        long actual = dateTime.ToUnixTimestamp();

        Assert.Equal(ticks, actual);
    }

    [Fact]
    public async Task ReadBytesAsync_CopiesStreamContents()
    {
        var content = "hello"u8.ToArray();
        using var input = new MemoryStream(content);

        var result = await input.ReadBytesAsync();

        Assert.Equal(content, result);
    }

    [Fact]
    public void ToNullableInt_ValidInteger_ReturnsParsedValue()
    {
        Assert.Equal(42, "42".ToNullableInt());
    }

    [Fact]
    public void ToNullableInt_InvalidInteger_ReturnsNull()
    {
        Assert.Null("not-a-number".ToNullableInt());
    }

    [Fact]
    public void GetWopiSrc_UnknownResourceType_Throws()
    {
        var url = new Moq.Mock<Microsoft.AspNetCore.Mvc.IUrlHelper>();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            url.Object.GetWopiSrc((WopiHost.Abstractions.WopiResourceType)999, "id"));
    }
}
