using WopiHost.Abstractions;

namespace WopiHost.Core.Tests.Abstractions;

public class WopiResourceIdTests
{
    [Fact]
    public void FromCanonicalPath_NullPath_Throws()
        => Assert.Throws<ArgumentNullException>(() => WopiResourceId.FromCanonicalPath(null!));

    [Fact]
    public void FromCanonicalPath_EmptyPath_ReturnsStableHash()
    {
        // Empty-string sentinel is how the Azure provider denotes the root container; the
        // helper must accept it and produce a deterministic 64-char lowercase hex SHA-256.
        var id1 = WopiResourceId.FromCanonicalPath(string.Empty);
        var id2 = WopiResourceId.FromCanonicalPath(string.Empty);

        Assert.Equal(id1, id2);
        Assert.Equal(64, id1.Length);
        Assert.All(id1, c => Assert.True(char.IsAsciiHexDigitLower(c)));
    }

    [Fact]
    public void FromCanonicalPath_IsDeterministic()
    {
        var first = WopiResourceId.FromCanonicalPath("docs/report.docx");
        var second = WopiResourceId.FromCanonicalPath("docs/report.docx");

        Assert.Equal(first, second);
    }

    [Fact]
    public void FromCanonicalPath_DoesNotCaseFold()
    {
        // The contract is that the *caller* applies case-folding when the underlying store
        // compares names case-insensitively. The helper hashes the bytes it was given,
        // unchanged. Two casings of the same path must produce different ids, otherwise
        // case-sensitive stores (like Azure Blob Storage) lose data.
        var lower = WopiResourceId.FromCanonicalPath("Foo/file.txt");
        var upper = WopiResourceId.FromCanonicalPath("FOO/FILE.TXT");

        Assert.NotEqual(lower, upper);
    }

    [Fact]
    public void FromCanonicalPath_OutputIsLowercaseHex()
    {
        var id = WopiResourceId.FromCanonicalPath("any/path/will/do.txt");

        Assert.Equal(64, id.Length);
        Assert.All(id, c => Assert.True(char.IsAsciiHexDigitLower(c)));
    }
}
