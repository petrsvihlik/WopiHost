namespace WopiHost.FileSystemProvider.Tests;

public class WopiContainerTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("WopiFolderTest_");

    public void Dispose()
    {
        _tempDir.Refresh();
        if (_tempDir.Exists) _tempDir.Delete(recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Identifier_ReturnsConstructorValue()
    {
        var sut = new WopiContainer(_tempDir.FullName, "folder-id");
        Assert.Equal("folder-id", sut.Identifier);
    }

    [Fact]
    public void Name_ReturnsLeafDirectoryName()
    {
        var sut = new WopiContainer(_tempDir.FullName, "folder-id");
        Assert.Equal(_tempDir.Name, sut.Name);
    }

    [Fact]
    public void Size_EmptyFolder_ReturnsZero()
    {
        var sut = new WopiContainer(_tempDir.FullName, "folder-id");
        Assert.Equal(0L, sut.Size);
    }

    [Fact]
    public void Size_SumsAllDescendantFiles_Recursive()
    {
        // Root file + nested file under a sub-folder; Size should include both.
        File.WriteAllBytes(Path.Join(_tempDir.FullName, "a.bin"), new byte[10]);
        var sub = _tempDir.CreateSubdirectory("sub");
        File.WriteAllBytes(Path.Join(sub.FullName, "b.bin"), new byte[25]);

        var sut = new WopiContainer(_tempDir.FullName, "folder-id");

        Assert.Equal(35L, sut.Size);
    }

    [Fact]
    public void Size_NonExistentFolder_ReturnsZeroRatherThanThrowing()
    {
        var sut = new WopiContainer(Path.Join(_tempDir.FullName, "missing"), "folder-id");
        Assert.Equal(0L, sut.Size);
    }

    [Fact]
    public void ChildCount_CountsDirectChildrenOnly_NotRecursive()
    {
        // 2 root files + 1 sub-folder = 3 direct children. The file inside the sub-folder
        // doesn't count — ChildCount is shallow per the IWopiContainer contract.
        File.WriteAllText(Path.Join(_tempDir.FullName, "a.txt"), "x");
        File.WriteAllText(Path.Join(_tempDir.FullName, "b.txt"), "x");
        var sub = _tempDir.CreateSubdirectory("sub");
        File.WriteAllText(Path.Join(sub.FullName, "nested.txt"), "x");

        var sut = new WopiContainer(_tempDir.FullName, "folder-id");

        Assert.Equal(3, sut.ChildCount);
    }

    [Fact]
    public void ChildCount_EmptyFolder_ReturnsZero()
    {
        var sut = new WopiContainer(_tempDir.FullName, "folder-id");
        Assert.Equal(0, sut.ChildCount);
    }

    [Fact]
    public void ChildCount_NonExistentFolder_ReturnsZeroRatherThanThrowing()
    {
        var sut = new WopiContainer(Path.Join(_tempDir.FullName, "missing"), "folder-id");
        Assert.Equal(0, sut.ChildCount);
    }
}
