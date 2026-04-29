using WopiHost.Core.Infrastructure;

namespace WopiHost.Core.Tests.Infrastructure;

public class UtfStringEdgeCaseTests
{
    [Fact]
    public void Parse_NullInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => UtfString.Parse(s: null!, provider: null));
    }

    [Fact]
    public void FromEncoded_NullInput_ReturnsInstanceWithNullValues()
    {
        var sut = UtfString.FromEncoded(null);

        Assert.Null(sut.ToString());
        Assert.Null(sut.ToString(asEncoded: true));
    }
}
