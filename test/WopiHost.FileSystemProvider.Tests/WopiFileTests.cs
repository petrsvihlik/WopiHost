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
    public void Owner_OnSupportedPlatform_ReturnsNonEmpty()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            Assert.False(string.IsNullOrEmpty(_sut.Owner));
        }
        else
        {
            // CA1416 suppressed: the surrounding else branch already guards
            // for non-Windows/non-Linux, but the analyzer can't follow that
            // through the lambda capture.
#pragma warning disable CA1416
            Assert.Throws<PlatformNotSupportedException>(() => _ = _sut.Owner);
#pragma warning restore CA1416
        }
    }

    [Fact]
    public void Owner_OnLinux_MatchesProcessUserName()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Files created by the test process are owned by the current user;
        // statx + getpwuid_r should resolve back to that name (or numeric uid
        // if the user has been deleted from /etc/passwd, which we don't expect
        // in a CI environment).
        Assert.Equal(Environment.UserName, _sut.Owner);
    }

    [Fact]
    public void Owner_OnLinux_FileDeleted_Throws()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Construct against a real file (FileVersionInfo.GetVersionInfo throws
        // FileNotFoundException for missing paths on Unix), then remove it so
        // statx fails with ENOENT and LinuxFileOwner surfaces that as IOException.
        var transient = Path.Combine(_tempDir.FullName, "transient.docx");
        File.WriteAllText(transient, "x");
        var sut = new WopiFile(transient, "id-transient");
        File.Delete(transient);

#pragma warning disable CA1416 // Linux-only test path; analyzer can't follow the early-return guard through the lambda
        Assert.ThrowsAny<IOException>(() => _ = sut.Owner);
#pragma warning restore CA1416
    }
}
