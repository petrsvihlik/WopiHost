using WopiHost.Discovery.Models;

namespace WopiHost.Discovery.Tests;

public class WopiProofKeysTests
{
    [Fact]
    public void Properties_RoundTrip()
    {
        var sut = new WopiProofKeys
        {
            Value = "v",
            OldValue = "ov",
            Modulus = "m",
            Exponent = "e",
            OldModulus = "om",
            OldExponent = "oe",
        };

        Assert.Equal("v", sut.Value);
        Assert.Equal("ov", sut.OldValue);
        Assert.Equal("m", sut.Modulus);
        Assert.Equal("e", sut.Exponent);
        Assert.Equal("om", sut.OldModulus);
        Assert.Equal("oe", sut.OldExponent);
    }
}
