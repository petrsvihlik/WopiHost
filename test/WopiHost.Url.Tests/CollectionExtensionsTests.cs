namespace WopiHost.Url.Tests;

public class CollectionExtensionsTests
{
    [Fact]
    public void Merge_WithNullArgument_ReturnsOther()
    {
        var full = new Dictionary<string, string>();
        Dictionary<string, string>? empty = null;

        var one = full.Merge(empty);
        var two = empty.Merge(full);

        Assert.Equal(full, one);
        Assert.Equal(full, two);
    }

    [Fact]
    public void Merge_TwoDictionaries_CombinesEntries()
    {
        var a = new Dictionary<string, string> { { "A", "B"}, { "C", "D" } };
        var b = new Dictionary<string, string> { { "G", "H" }, { "I", "J" } };

        var result = a.Merge(b);

        Assert.NotNull(result);
        Assert.Contains("A", result);
        Assert.Contains("G", result);
    }
}
