using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace WopiHost.AzureStorageProvider.Tests;

public class BlobIdMapTests
{
    private static BlobIdMap NewMap() => new(NullLogger<BlobIdMap>.Instance);

    [Fact]
    public void IdFromPath_DeterministicAndCaseInsensitive()
    {
        var lower = BlobIdMap.IdFromPath("a/b/file.txt");
        var upper = BlobIdMap.IdFromPath("A/B/FILE.TXT");

        Assert.Equal(lower, upper);
        // Hex MD5 — 32 lowercase hex chars.
        Assert.Equal(32, lower.Length);
        Assert.All(lower, c => Assert.True(char.IsAsciiHexDigitLower(c)));
    }

    [Fact]
    public void Add_RegistersPath_AndReturnsDeterministicId()
    {
        var map = NewMap();
        var id = map.Add("docs/report.docx");

        Assert.Equal(BlobIdMap.IdFromPath("docs/report.docx"), id);
        Assert.True(map.TryGetPath(id, out var path));
        Assert.Equal("docs/report.docx", path);
    }

    [Fact]
    public void TryGetPath_UnknownId_ReturnsFalse()
    {
        var map = NewMap();
        Assert.False(map.TryGetPath("does-not-exist", out var path));
        Assert.Null(path);
    }

    [Fact]
    public void TryGetFileId_UnknownPath_ReturnsFalse()
    {
        var map = NewMap();
        map.Add("present.txt");

        Assert.False(map.TryGetFileId("absent.txt", out var id));
        Assert.Null(id);
    }

    [Fact]
    public void TryGetFileId_KnownPath_ReturnsId()
    {
        var map = NewMap();
        var addedId = map.Add("present.txt");

        Assert.True(map.TryGetFileId("present.txt", out var id));
        Assert.Equal(addedId, id);
    }

    [Fact]
    public void Remove_DropsMapping()
    {
        var map = NewMap();
        var id = map.Add("doomed.txt");

        Assert.True(map.Remove(id));
        Assert.False(map.TryGetPath(id, out _));
        Assert.False(map.Remove(id)); // second call returns false
    }

    [Fact]
    public void Update_RetainsIdentifier_PointsAtNewPath()
    {
        var map = NewMap();
        var id = map.Add("before.txt");

        map.Update(id, "after.txt");

        Assert.True(map.TryGetPath(id, out var path));
        Assert.Equal("after.txt", path);
    }

    [Fact]
    public void ScanAll_EmptyEnumeration_RegistersOnlyRoot()
    {
        var map = NewMap();
        map.ScanAll(Array.Empty<string>());

        Assert.True(map.WasScanned);
        var rootId = BlobIdMap.IdFromPath(string.Empty);
        Assert.True(map.TryGetPath(rootId, out var rootPath));
        Assert.Equal(string.Empty, rootPath);
    }

    [Fact]
    public void ScanAll_FlatBlobs_RegistersEachBlob()
    {
        var map = NewMap();
        map.ScanAll(["a.txt", "b.docx", "c.pdf"]);

        Assert.True(map.TryGetFileId("a.txt", out _));
        Assert.True(map.TryGetFileId("b.docx", out _));
        Assert.True(map.TryGetFileId("c.pdf", out _));
    }

    [Fact]
    public void ScanAll_NestedBlobs_RegistersAllAncestorFolders()
    {
        var map = NewMap();
        map.ScanAll(["a/b/c/leaf.txt"]);

        // Leaf
        Assert.True(map.TryGetFileId("a/b/c/leaf.txt", out _));
        // Each intermediate folder is registered
        Assert.True(map.TryGetFileId("a/b/c", out _));
        Assert.True(map.TryGetFileId("a/b", out _));
        Assert.True(map.TryGetFileId("a", out _));
    }

    [Fact]
    public void ScanAll_FolderMarker_RegistersFolder_HidesMarker()
    {
        var map = NewMap();
        map.ScanAll(["empty/" + BlobIdMap.FolderMarker]);

        Assert.True(map.TryGetFileId("empty", out _));
        Assert.False(map.TryGetFileId("empty/" + BlobIdMap.FolderMarker, out _));
    }

    [Fact]
    public void ScanAll_FolderMarkerOnRoot_RegistersOnlyRoot()
    {
        var map = NewMap();
        // A bare folder-marker at root level (not nested under any prefix).
        map.ScanAll([BlobIdMap.FolderMarker]);

        Assert.False(map.TryGetFileId(BlobIdMap.FolderMarker, out _));
        // Root is always registered.
        Assert.True(map.TryGetPath(BlobIdMap.IdFromPath(string.Empty), out _));
    }

    [Fact]
    public void ScanAll_DedupesAncestorRegistrations()
    {
        var map = NewMap();
        map.ScanAll(["a/file1.txt", "a/file2.txt", "a/sub/file3.txt"]);

        Assert.True(map.TryGetFileId("a", out var firstAId));
        Assert.True(map.TryGetFileId("a", out var secondAId));
        Assert.Equal(firstAId, secondAId);
        Assert.True(map.TryGetFileId("a/sub", out _));
    }

    [Fact]
    public void ScanAll_ResetsExistingMap()
    {
        var map = NewMap();
        map.Add("orphan.txt");

        map.ScanAll(["fresh.txt"]);

        Assert.False(map.TryGetFileId("orphan.txt", out _));
        Assert.True(map.TryGetFileId("fresh.txt", out _));
    }

}
