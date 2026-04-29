using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

public class UtfStringTests
{
    const string encodedValue = "madeup+AF8-name.wopitestx";
    const string decodedValue = "madeup_name.wopitestx";

    [Fact]
    public void FromDecoded_ShouldReturnCorrectEncodedValue()
    {
        UtfString utfString = UtfString.FromDecoded(decodedValue);

        Assert.Equal(encodedValue, utfString.ToString(true));
        Assert.Equal(decodedValue, utfString.ToString(false));
    }

    [Fact]
    public void FromEncoded_ShouldReturnCorrectDecodedValue()
    {
        UtfString utfString = UtfString.FromEncoded(encodedValue);

        Assert.Equal(encodedValue, utfString.ToString(true));
        Assert.Equal(decodedValue, utfString.ToString(false));
    }

    [Fact]
    public void Parse_ShouldReturnCorrectUtfString()
    {
        UtfString utfString = UtfString.Parse(encodedValue, null);

        Assert.Equal(encodedValue, utfString.ToString(true));
        Assert.Equal(decodedValue, utfString.ToString(false));
    }

    [Fact]
    public void TryParse_ShouldReturnTrueForValidString()
    {
        bool result = UtfString.TryParse(encodedValue, null, out UtfString utfString);

        Assert.True(result);
        Assert.Equal(encodedValue, utfString.ToString(true));
        Assert.Equal(decodedValue, utfString.ToString(false));
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
        UtfString utfString = UtfString.FromDecoded(decodedValue);

        string result = utfString;

        Assert.Equal(decodedValue, result);
    }
}
