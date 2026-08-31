namespace WopiHost.Url.Tests;

public class CollectionExtensionsTests
{
    [Fact]
    public void Merge_WithNullArgument_ReturnsOtherInstance()
    {
        var populated = new Dictionary<string, string> { ["A"] = "B" };
        Dictionary<string, string>? nullDict = null;

        Assert.Same(populated, populated.Merge(nullDict));
        Assert.Same(populated, nullDict.Merge(populated));
    }

    [Fact]
    public void Merge_TwoDictionaries_CombinesAllEntries()
    {
        var a = new Dictionary<string, string> { { "A", "B" }, { "C", "D" } };
        var b = new Dictionary<string, string> { { "G", "H" }, { "I", "J" } };

        var result = a.Merge(b);

        Assert.Equal(new Dictionary<string, string>
        {
            ["A"] = "B",
            ["C"] = "D",
            ["G"] = "H",
            ["I"] = "J",
        }, result);
    }

    [Fact]
    public void Merge_DuplicateKey_FirstDictionaryWins()
    {
        // The documented contract: "If duplicate occurs, dictA wins over dictB."
        var a = new Dictionary<string, string> { ["K"] = "from-a", ["X"] = "1" };
        var b = new Dictionary<string, string> { ["K"] = "from-b", ["Y"] = "2" };

        var result = a.Merge(b);

        Assert.Equal(new Dictionary<string, string>
        {
            ["K"] = "from-a",
            ["X"] = "1",
            ["Y"] = "2",
        }, result);
    }
}
