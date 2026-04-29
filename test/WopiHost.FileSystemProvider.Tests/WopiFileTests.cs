namespace WopiHost.FileSystemProvider.Tests;

public class WopiFileTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("WopiFileTest_");
    private readonly string _filePath;
    private readonly WopiFile _sut;

    public WopiFileTests()
    {
        _filePath = Path.Combine(_tempDir.FullName, "doc.docx");
        File.WriteAllText(_filePath, "hello world");
        _sut = new WopiFile(_filePath, "id-1");
    }

    public void Dispose()
    {
        _tempDir.Refresh();
        if (_tempDir.Exists) _tempDir.Delete(recursive: true);
    }

    [Fact]
    public void Identifier_ReturnsConstructorValue() => Assert.Equal("id-1", _sut.Identifier);

    [Fact]
    public void Exists_True_WhenFilePresent() => Assert.True(_sut.Exists);

    [Fact]
    public void Extension_ReturnsExtensionWithoutDot() => Assert.Equal("docx", _sut.Extension);

    [Fact]
    public void Version_ReturnsNonNullForNonExecutable()
    {
        // .docx is not a PE/version-stamped binary so FileVersion is null;
        // the provider falls back to LastWriteTimeUtc.Ticks.
        Assert.NotNull(_sut.Version);
        Assert.NotEmpty(_sut.Version!);
    }

    [Fact]
    public void Checksum_DefaultsToNull() => Assert.Null(_sut.Checksum);

    [Fact]
    public void Size_MatchesFileLength() => Assert.Equal(new FileInfo(_filePath).Length, _sut.Size);

    [Fact]
    public void Length_MatchesFileLength() => Assert.Equal(new FileInfo(_filePath).Length, _sut.Length);

    [Fact]
    public void Name_ExcludesExtension() => Assert.Equal("doc", _sut.Name);

    [Fact]
    public void LastWriteTimeUtc_MatchesFileInfo()
        => Assert.Equal(new FileInfo(_filePath).LastWriteTimeUtc, _sut.LastWriteTimeUtc);

    [Fact]
    public async Task GetReadStream_ReturnsReadableStream()
    {
        await using var stream = await _sut.GetReadStream();
        using var reader = new StreamReader(stream);
        Assert.Equal("hello world", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task GetWriteStream_TruncatesAndAllowsWrite()
    {
        await using (var stream = await _sut.GetWriteStream())
        await using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync("new");
        }
        Assert.Equal("new", File.ReadAllText(_filePath));
    }

    [Fact]
    public void Owner_ReturnsNonNull()
    {
        // The Windows branch returns the NT account; the non-Windows branch
        // returns the literal "UNSUPPORTED_PLATFORM". Both must be non-null.
#pragma warning disable CA1416 // Owner is annotated for Windows/Linux only; the impl returns a fallback string elsewhere
        Assert.NotNull(_sut.Owner);
#pragma warning restore CA1416
    }
}
