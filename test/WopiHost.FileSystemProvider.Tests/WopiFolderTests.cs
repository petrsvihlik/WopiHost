namespace WopiHost.FileSystemProvider.Tests;

public class WopiFolderTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("WopiFolderTest_");

    public void Dispose()
    {
        _tempDir.Refresh();
        if (_tempDir.Exists) _tempDir.Delete(recursive: true);
    }

    [Fact]
    public void Identifier_ReturnsConstructorValue()
    {
        var sut = new WopiFolder(_tempDir.FullName, "folder-id");
        Assert.Equal("folder-id", sut.Identifier);
    }

    [Fact]
    public void Name_ReturnsLeafDirectoryName()
    {
        var sut = new WopiFolder(_tempDir.FullName, "folder-id");
        Assert.Equal(_tempDir.Name, sut.Name);
    }
}
