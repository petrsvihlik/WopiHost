using Microsoft.Extensions.Logging.Abstractions;

namespace WopiHost.FileSystemProvider.Tests;

public class InMemoryFileIdsTests : IDisposable
{
    private readonly InMemoryFileIds _sut = new(NullLogger<InMemoryFileIds>.Instance);
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("WopiTest_");

    public void Dispose() => _tempDir.Delete(recursive: true);

    [Fact]
    public void ScanAll_SamePath_ProducesSameIds()
    {
        // Act – scan the same directory twice
        _sut.ScanAll(_tempDir.FullName);
        _sut.TryGetFileId(_tempDir.FullName, out var id1);

        _sut.ScanAll(_tempDir.FullName);
        _sut.TryGetFileId(_tempDir.FullName, out var id2);

        // Assert
        Assert.NotNull(id1);
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void AddFile_SamePath_ReturnsSameId()
    {
        // Arrange
        var path = Path.Combine(_tempDir.FullName, "test.docx");

        // Act
        var id1 = _sut.AddFile(path);
        var id2 = _sut.AddFile(path);

        // Assert
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void AddFile_DifferentPaths_ReturnsDifferentIds()
    {
        // Arrange
        var path1 = Path.Combine(_tempDir.FullName, "file1.docx");
        var path2 = Path.Combine(_tempDir.FullName, "file2.docx");

        // Act
        var id1 = _sut.AddFile(path1);
        var id2 = _sut.AddFile(path2);

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ScanAll_FileInSubdirectory_CanBeFoundById()
    {
        // Arrange
        var subDir = _tempDir.CreateSubdirectory("sub");
        var filePath = Path.Combine(subDir.FullName, "doc.docx");
        File.WriteAllText(filePath, string.Empty);

        // Act
        _sut.ScanAll(_tempDir.FullName);
        var found = _sut.TryGetFileId(filePath, out var fileId);

        // Assert
        Assert.True(found);
        Assert.NotNull(fileId);
        Assert.True(_sut.TryGetPath(fileId, out var resolvedPath));
        Assert.Equal(filePath, resolvedPath);
    }
}
