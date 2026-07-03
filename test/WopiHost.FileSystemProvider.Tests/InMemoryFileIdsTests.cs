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

    [Fact]
    public void UpdateFile_OldPath_NoLongerResolvesViaReverseLookup()
    {
        // The path→id reverse map must drop the old binding when an id is rebound to a new
        // path, otherwise stale entries pile up unboundedly.
        var oldPath = Path.Combine(_tempDir.FullName, "old.docx");
        var newPath = Path.Combine(_tempDir.FullName, "new.docx");
        var id = _sut.AddFile(oldPath);

        _sut.UpdateFile(id, newPath);

        Assert.False(_sut.TryGetFileId(oldPath, out _));
        Assert.True(_sut.TryGetFileId(newPath, out var resolvedId));
        Assert.Equal(id, resolvedId);
    }

    [Fact]
    public void RemoveId_Path_NoLongerResolvesViaReverseLookup()
    {
        var path = Path.Combine(_tempDir.FullName, "doc.docx");
        var id = _sut.AddFile(path);

        _sut.RemoveId(id);

        Assert.False(_sut.TryGetFileId(path, out _));
    }

    [Fact]
    public void ScanAll_Rescan_DropsBindingsForRemovedFiles()
    {
        // Confirm both maps are reset on rescan — clearing only the forward map would let a
        // parallel reverse-map entry linger.
        var stalePath = Path.Combine(_tempDir.FullName, "stale.docx");
        File.WriteAllText(stalePath, string.Empty);
        _sut.ScanAll(_tempDir.FullName);
        Assert.True(_sut.TryGetFileId(stalePath, out _));

        File.Delete(stalePath);
        _sut.ScanAll(_tempDir.FullName);

        Assert.False(_sut.TryGetFileId(stalePath, out _));
    }

    [Fact]
    public async Task AddFile_ConcurrentDistinctPaths_AllEntriesObservable()
    {
        // A non-concurrent Dictionary would let parallel writers corrupt the bucket array or
        // lose entries silently. Verify each parallel add survives and is reachable in both
        // directions.
        const int writers = 64;
        var paths = Enumerable.Range(0, writers)
            .Select(i => Path.Combine(_tempDir.FullName, $"f{i}.docx"))
            .ToArray();

        var ids = await Task.WhenAll(
            paths.Select(p => Task.Run(() => _sut.AddFile(p))));

        Assert.Equal(writers, ids.Distinct().Count());
        for (var i = 0; i < writers; i++)
        {
            Assert.True(_sut.TryGetFileId(paths[i], out var idViaPath));
            Assert.Equal(ids[i], idViaPath);
            Assert.True(_sut.TryGetPath(ids[i], out var pathViaId));
            Assert.Equal(paths[i], pathViaId);
        }
    }

    [Fact]
    public void GetOrAddFileId_UnknownPath_RegistersAndRoundTrips()
    {
        var path = Path.Combine(_tempDir.FullName, "late.docx");

        var id = _sut.GetOrAddFileId(path);

        Assert.True(_sut.TryGetPath(id, out var resolved));
        Assert.Equal(path, resolved);
        Assert.Equal(id, _sut.GetOrAddFileId(path));
    }

    [Fact]
    public void GetOrAddFileId_ReboundPath_ReturnsRetainedId()
    {
        // After a rename the original id stays bound to the new path (the live WOPI session
        // keeps using it). GetOrAddFileId must return that retained id, not re-derive a fresh
        // one from the new path and clobber the binding.
        var oldPath = Path.Combine(_tempDir.FullName, "old.docx");
        var newPath = Path.Combine(_tempDir.FullName, "new.docx");
        var id = _sut.AddFile(oldPath);
        _sut.UpdateFile(id, newPath);

        Assert.Equal(id, _sut.GetOrAddFileId(newPath));
    }

    [Fact]
    public void TryResolveByScan_UnmappedFile_ResolvesAndRegisters()
    {
        // The id another process would derive for a file this map has never seen.
        var path = Path.Combine(_tempDir.FullName, "unseen.docx");
        File.WriteAllText(path, string.Empty);
        var peerDerivedId = _sut.AddFile(path);
        _sut.RemoveId(peerDerivedId); // forget it again — only the disk knows the file now

        var found = _sut.TryResolveByScan(_tempDir.FullName, peerDerivedId, out var resolved);

        Assert.True(found);
        Assert.Equal(path, resolved);
        Assert.True(_sut.TryGetPath(peerDerivedId, out _)); // registered, next lookup is O(1)
    }

    [Fact]
    public void TryResolveByScan_ReboundPath_KeepsCanonicalReverseBinding()
    {
        // A rename retained old-id for the new path; a peer-derived id for the same path must
        // resolve as an alias without stealing the path's canonical (retained) id.
        var oldPath = Path.Combine(_tempDir.FullName, "old.docx");
        var newPath = Path.Combine(_tempDir.FullName, "renamed.docx");
        File.WriteAllText(newPath, string.Empty);
        var retainedId = _sut.AddFile(oldPath);
        _sut.UpdateFile(retainedId, newPath);
        var peerDerivedId = new InMemoryFileIds(NullLogger<InMemoryFileIds>.Instance).AddFile(newPath);

        Assert.True(_sut.TryResolveByScan(_tempDir.FullName, peerDerivedId, out var resolved));

        Assert.Equal(newPath, resolved);
        Assert.True(_sut.TryGetPath(retainedId, out _));            // alias didn't evict the retained id
        Assert.True(_sut.TryGetFileId(newPath, out var canonical));
        Assert.Equal(retainedId, canonical);                        // reverse map still canonical
    }

    [Fact]
    public void TryResolveByScan_UnknownId_ReturnsFalse()
    {
        Assert.False(_sut.TryResolveByScan(_tempDir.FullName, "not-a-real-id", out _));
    }

    [Fact]
    public void TryResolveByScan_MissDebouncesSubsequentScans()
    {
        // A failed scan suppresses the next one for the debounce window, so within it even a
        // resolvable id reports a miss (the caller surfaces 404 and the client's retry lands
        // after the window).
        var path = Path.Combine(_tempDir.FullName, "debounced.docx");
        File.WriteAllText(path, string.Empty);
        var resolvableId = _sut.AddFile(path);
        _sut.RemoveId(resolvableId); // only the disk knows the file now

        Assert.False(_sut.TryResolveByScan(_tempDir.FullName, "garbage-id", out _));
        Assert.False(_sut.TryResolveByScan(_tempDir.FullName, resolvableId, out _));
    }

    [Fact]
    public async Task Mixed_ConcurrentAddAndRemove_LeavesConsistentState()
    {
        // Stress: half the workers add, the other half remove the same file id. Whatever the
        // interleaving, the forward and reverse maps must agree (no dangling reverse entry).
        const int rounds = 200;
        var path = Path.Combine(_tempDir.FullName, "shared.docx");

        var add = Task.Run(() =>
        {
            for (var i = 0; i < rounds; i++)
            {
                _sut.AddFile(path);
            }
        });
        var remove = Task.Run(() =>
        {
            for (var i = 0; i < rounds; i++)
            {
                _sut.TryGetFileId(path, out var id);
                if (id is not null)
                {
                    _sut.RemoveId(id);
                }
            }
        });
        await Task.WhenAll(add, remove);

        // Final consistency check: every id present must round-trip through the reverse map and
        // back. A leaked reverse entry would resolve to an id that's no longer in the forward map.
        if (_sut.TryGetFileId(path, out var finalId))
        {
            Assert.True(_sut.TryGetPath(finalId, out var roundTripPath));
            Assert.Equal(path, roundTripPath);
        }
    }
}
