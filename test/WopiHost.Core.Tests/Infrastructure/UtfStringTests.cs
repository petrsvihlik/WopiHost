using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

public class UtfStringTests
{
    const string EncodedValue = "madeup+AF8-name.wopitestx";
    const string DecodedValue = "madeup_name.wopitestx";

    [Fact]
    public void FromDecoded_ShouldReturnCorrectEncodedValue()
    {
        UtfString utfString = UtfString.FromDecoded(DecodedValue);

        Assert.Equal(EncodedValue, utfString.ToString(true));
        Assert.Equal(DecodedValue, utfString.ToString(false));
    }

    [Fact]
    public void FromEncoded_ShouldReturnCorrectDecodedValue()
    {
        UtfString utfString = UtfString.FromEncoded(EncodedValue);

        Assert.Equal(EncodedValue, utfString.ToString(true));
        Assert.Equal(DecodedValue, utfString.ToString(false));
    }

    [Fact]
    public void Parse_ShouldReturnCorrectUtfString()
    {
        UtfString utfString = UtfString.Parse(EncodedValue, null);

        Assert.Equal(EncodedValue, utfString.ToString(true));
        Assert.Equal(DecodedValue, utfString.ToString(false));
    }

    [Fact]
    public void TryParse_ShouldReturnTrueForValidString()
    {
        bool result = UtfString.TryParse(EncodedValue, null, out UtfString utfString);

        Assert.True(result);
        Assert.Equal(EncodedValue, utfString.ToString(true));
        Assert.Equal(DecodedValue, utfString.ToString(false));
    }

    [Fact]
    public void TryParse_ShouldReturnFalseForNullString()
    {
        bool result = UtfString.TryParse(null, null, out UtfString utfString);

        Assert.False(result);
        Assert.Null(utfString.ToString(true));
        Assert.Null(utfString.ToString(false));
    }

    [Fact]
    public void ImplicitConversion_ShouldReturnDecodedValue()
    {
        UtfString utfString = UtfString.FromDecoded(DecodedValue);

        string result = utfString;

        Assert.Equal(DecodedValue, result);
    }
}
