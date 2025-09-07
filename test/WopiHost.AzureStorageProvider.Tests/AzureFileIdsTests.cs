using Microsoft.Extensions.Logging;
using Moq;

namespace WopiHost.AzureStorageProvider.Tests;

public class AzureFileIdsTests
{
    private readonly Mock<ILogger<AzureFileIds>> _mockLogger;
    private readonly AzureFileIds _fileIds;

    public AzureFileIdsTests()
    {
        _mockLogger = new Mock<ILogger<AzureFileIds>>();
        _fileIds = new AzureFileIds(_mockLogger.Object);
    }

    [Fact]
    public void AddFile_ShouldReturnUniqueId()
    {
        // Arrange
        var blobPath = "test/file.txt";

        // Act
        var id1 = _fileIds.AddFile(blobPath);
        var id2 = _fileIds.AddFile(blobPath);

        // Assert
        Assert.NotEmpty(id1);
        Assert.NotEmpty(id2);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void TryGetFileId_WithExistingPath_ShouldReturnTrue()
    {
        // Arrange
        var blobPath = "test/file.txt";
        var id = _fileIds.AddFile(blobPath);

        // Act
        var result = _fileIds.TryGetFileId(blobPath, out var retrievedId);

        // Assert
        Assert.True(result);
        Assert.Equal(id, retrievedId);
    }

    [Fact]
    public void TryGetFileId_WithNonExistentPath_ShouldReturnFalse()
    {
        // Act
        var result = _fileIds.TryGetFileId("nonexistent/path.txt", out var retrievedId);

        // Assert
        Assert.False(result);
        Assert.Null(retrievedId);
    }

    [Fact]
    public void TryGetPath_WithExistingId_ShouldReturnTrue()
    {
        // Arrange
        var blobPath = "test/file.txt";
        var id = _fileIds.AddFile(blobPath);

        // Act
        var result = _fileIds.TryGetPath(id, out var retrievedPath);

        // Assert
        Assert.True(result);
        Assert.Equal(blobPath, retrievedPath);
    }

    [Fact]
    public void TryGetPath_WithNonExistentId_ShouldReturnFalse()
    {
        // Act
        var result = _fileIds.TryGetPath("nonexistent-id", out var retrievedPath);

        // Assert
        Assert.False(result);
        Assert.Null(retrievedPath);
    }

    [Fact]
    public void GetPath_WithExistingId_ShouldReturnPath()
    {
        // Arrange
        var blobPath = "test/file.txt";
        var id = _fileIds.AddFile(blobPath);

        // Act
        var result = _fileIds.GetPath(id);

        // Assert
        Assert.Equal(blobPath, result);
    }

    [Fact]
    public void GetPath_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var result = _fileIds.GetPath("nonexistent-id");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void RemoveId_ShouldRemoveBothMappings()
    {
        // Arrange
        var blobPath = "test/file.txt";
        var id = _fileIds.AddFile(blobPath);

        // Act
        _fileIds.RemoveId(id);

        // Assert
        Assert.False(_fileIds.TryGetPath(id, out _));
        Assert.False(_fileIds.TryGetFileId(blobPath, out _));
    }

    [Fact]
    public void UpdateFile_ShouldUpdateBothMappings()
    {
        // Arrange
        var oldPath = "test/old-file.txt";
        var newPath = "test/new-file.txt";
        var id = _fileIds.AddFile(oldPath);

        // Act
        _fileIds.UpdateFile(id, newPath);

        // Assert
        Assert.Equal(newPath, _fileIds.GetPath(id));
        Assert.Equal(id, _fileIds.TryGetFileId(newPath, out var retrievedId) ? retrievedId : null);
        Assert.False(_fileIds.TryGetFileId(oldPath, out _));
    }

    [Fact]
    public void WasScanned_Initially_ShouldBeFalse()
    {
        // Assert
        Assert.False(_fileIds.WasScanned);
    }
}
