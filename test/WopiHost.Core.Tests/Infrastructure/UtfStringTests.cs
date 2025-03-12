using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

public class UtfStringTests
{
    const string encodedValue = "madeup+AF8-name.wopitestx";
    const string decodedValue = "madeup_name.wopitestx";

    [Fact]
    public void FromDecoded_ShouldReturnCorrectEncodedValue()
    {
        // Act
        UtfString utfString = UtfString.FromDecoded(decodedValue);

        // Assert
        Assert.Equal(encodedValue, utfString.ToString(true));
        Assert.Equal(decodedValue, utfString.ToString(false));
    }

    [Fact]
    public void FromEncoded_ShouldReturnCorrectDecodedValue()
    {
        // Act
        UtfString utfString = UtfString.FromEncoded(encodedValue);

        // Assert
        Assert.Equal(encodedValue, utfString.ToString(true));
        Assert.Equal(decodedValue, utfString.ToString(false));
    }

    [Fact]
    public void Parse_ShouldReturnCorrectUtfString()
    {
        // Act
        UtfString utfString = UtfString.Parse(encodedValue, null);

        // Assert
        Assert.Equal(encodedValue, utfString.ToString(true));
        Assert.Equal(decodedValue, utfString.ToString(false));
    }

    [Fact]
    public void TryParse_ShouldReturnTrueForValidString()
    {
        // Act
        bool result = UtfString.TryParse(encodedValue, null, out UtfString utfString);

        // Assert
        Assert.True(result);
        Assert.Equal(encodedValue, utfString.ToString(true));
        Assert.Equal(decodedValue, utfString.ToString(false));
    }

    [Fact]
    public void TryParse_ShouldReturnFalseForNullString()
    {
        // Act
        bool result = UtfString.TryParse(null, null, out UtfString utfString);

        // Assert
        Assert.False(result);
        Assert.Null(utfString.ToString(true));
        Assert.Null(utfString.ToString(false));
    }

    [Fact]
    public void ImplicitConversion_ShouldReturnDecodedValue()
    {
        // Arrange
        UtfString utfString = UtfString.FromDecoded(decodedValue);

        // Act
        string result = utfString;

        // Assert
        Assert.Equal(decodedValue, result);
    }
}
