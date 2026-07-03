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
        GC.SuppressFinalize(this);
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
    public void Constructor_MissingPath_DoesNotThrow_AndReportsNotExists()
    {
        // FileVersionInfo is read lazily so a WopiFile can represent an absent path (a stale
        // id→path map entry after a rename/delete) without faulting in CheckFileInfo. The ctor
        // must not throw, and the file degrades to Exists=false with a best-effort Version.
        var missing = Path.Combine(_tempDir.FullName, "gone.docx");
        var sut = new WopiFile(missing, "id-missing");

        Assert.False(sut.Exists);
        Assert.Equal("docx", sut.Extension);
        Assert.Equal("gone", sut.Name);
        Assert.NotNull(sut.Version); // falls back to LastWriteTimeUtc.Ticks
    }

    [Fact]
    public void Length_MatchesFileLength() => Assert.Equal(new FileInfo(_filePath).Length, _sut.Length);

    [Fact]
    public void Name_ExcludesExtension() => Assert.Equal("doc", _sut.Name);

    [Fact]
    public void LastWriteTimeUtc_MatchesFileInfo()
        => Assert.Equal(new FileInfo(_filePath).LastWriteTimeUtc, _sut.LastWriteTimeUtc);

    [Fact]
    public async Task OpenReadAsync_ReturnsReadableStream()
    {
        await using var stream = await _sut.OpenReadAsync();
        using var reader = new StreamReader(stream);
        Assert.Equal("hello world", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task OpenWriteAsync_TruncatesAndAllowsWrite()
    {
        await using (var stream = await _sut.OpenWriteAsync())
        await using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync("new");
        }
        Assert.Equal("new", File.ReadAllText(_filePath));
    }

    [Fact]
    public void Owner_OnSupportedPlatform_ReturnsNonEmpty()
    {
        // Windows (ACL owner), Linux (statx) and macOS (stat) all resolve a real owner.
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            Assert.False(string.IsNullOrEmpty(_sut.Owner));
        }
        else
        {
            // No ownership lookup is wired up for other platforms, so the contract
            // degrades to empty rather than throwing PlatformNotSupportedException.
            Assert.Equal(string.Empty, _sut.Owner);
        }
    }

    [Fact]
    public void Owner_OnUnix_MatchesProcessUserName()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        // Files created by the test process are owned by the current user;
        // stat/statx + getpwuid_r should resolve back to that name (or numeric uid
        // if the user has been deleted from the passwd db, which is not expected
        // in a CI environment).
        Assert.Equal(Environment.UserName, _sut.Owner);
    }

    [Fact]
    public void Owner_OnUnix_FileDeleted_ReturnsEmpty()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        // Construct against a real file, then remove it so stat/statx fails with ENOENT. The
        // owner helper surfaces that as IOException, which the Owner getter swallows to honour
        // the best-effort contract.
        var transient = Path.Combine(_tempDir.FullName, "transient.docx");
        File.WriteAllText(transient, "x");
        var sut = new WopiFile(transient, "id-transient");
        File.Delete(transient);

        Assert.Equal(string.Empty, sut.Owner);
    }
}
