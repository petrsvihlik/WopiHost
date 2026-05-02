using Microsoft.Extensions.Logging.Abstractions;

namespace WopiHost.FileSystemProvider.Tests;

public class InMemoryFileIdsTests : IDisposable
{
    private readonly InMemoryFileIds _sut = new(NullLogger<InMemoryFileIds>.Instance);
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("WopiTest_");

    public void Dispose()
    {
        _tempDir.Delete(recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ScanAll_SamePath_ProducesSameIds()
    {
        // scan the same directory twice
        _sut.ScanAll(_tempDir.FullName);
        _sut.TryGetFileId(_tempDir.FullName, out var id1);

        _sut.ScanAll(_tempDir.FullName);
        _sut.TryGetFileId(_tempDir.FullName, out var id2);

        Assert.NotNull(id1);
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void AddFile_SamePath_ReturnsSameId()
    {
        var path = Path.Combine(_tempDir.FullName, "test.docx");

        var id1 = _sut.AddFile(path);
        var id2 = _sut.AddFile(path);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void AddFile_DifferentPaths_ReturnsDifferentIds()
    {
        var path1 = Path.Combine(_tempDir.FullName, "file1.docx");
        var path2 = Path.Combine(_tempDir.FullName, "file2.docx");

        var id1 = _sut.AddFile(path1);
        var id2 = _sut.AddFile(path2);

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ScanAll_FileInSubdirectory_CanBeFoundById()
    {
        var subDir = _tempDir.CreateSubdirectory("sub");
        var filePath = Path.Combine(subDir.FullName, "doc.docx");
        File.WriteAllText(filePath, string.Empty);

        _sut.ScanAll(_tempDir.FullName);
        var found = _sut.TryGetFileId(filePath, out var fileId);

        Assert.True(found);
        Assert.NotNull(fileId);
        Assert.True(_sut.TryGetPath(fileId, out var resolvedPath));
        Assert.Equal(filePath, resolvedPath);
    }

    [Fact]
    public void WasScanned_FalseUntilScan()
    {
        Assert.False(_sut.WasScanned);
        _sut.ScanAll(_tempDir.FullName);
        Assert.True(_sut.WasScanned);
    }

    [Fact]
    public void GetPath_KnownId_ReturnsPath()
    {
        var path = Path.Combine(_tempDir.FullName, "doc.docx");
        var id = _sut.AddFile(path);

        Assert.Equal(path, _sut.GetPath(id));
    }

    [Fact]
    public void GetPath_UnknownId_ReturnsNull()
    {
        Assert.Null(_sut.GetPath("unknown-id"));
    }

    [Fact]
    public void GetPath_NullOrEmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => _sut.GetPath(""));
        Assert.Throws<ArgumentNullException>(() => _sut.GetPath(null!));
    }

    [Fact]
    public void RemoveId_RemovesEntry()
    {
        var path = Path.Combine(_tempDir.FullName, "doc.docx");
        var id = _sut.AddFile(path);

        _sut.RemoveId(id);

        Assert.False(_sut.TryGetPath(id, out _));
    }

    [Fact]
    public void UpdateFile_ChangesPathForExistingId()
    {
        var oldPath = Path.Combine(_tempDir.FullName, "old.docx");
        var newPath = Path.Combine(_tempDir.FullName, "new.docx");
        var id = _sut.AddFile(oldPath);

        _sut.UpdateFile(id, newPath);

        Assert.True(_sut.TryGetPath(id, out var resolved));
        Assert.Equal(newPath, resolved);
    }

    [Fact]
    public void ScanAll_WopiTestFile_GetsWopitestIdentifier()
    {
        var path = Path.Combine(_tempDir.FullName, "test.wopitest");
        File.WriteAllText(path, string.Empty);

        _sut.ScanAll(_tempDir.FullName);

        Assert.True(_sut.TryGetPath("WOPITEST", out var resolved));
        Assert.Equal(path, resolved);
    }
}
