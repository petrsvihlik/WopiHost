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
        var path = Path.Join(_tempDir.FullName, "test.docx");

        var id1 = _sut.AddFile(path);
        var id2 = _sut.AddFile(path);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void AddFile_DifferentPaths_ReturnsDifferentIds()
    {
        var path1 = Path.Join(_tempDir.FullName, "file1.docx");
        var path2 = Path.Join(_tempDir.FullName, "file2.docx");

        var id1 = _sut.AddFile(path1);
        var id2 = _sut.AddFile(path2);

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void ScanAll_FileInSubdirectory_CanBeFoundById()
    {
        var subDir = _tempDir.CreateSubdirectory("sub");
        var filePath = Path.Join(subDir.FullName, "doc.docx");
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
        var path = Path.Join(_tempDir.FullName, "doc.docx");
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
        var path = Path.Join(_tempDir.FullName, "doc.docx");
        var id = _sut.AddFile(path);

        _sut.RemoveId(id);

        Assert.False(_sut.TryGetPath(id, out _));
    }

    [Fact]
    public void UpdateFile_ChangesPathForExistingId()
    {
        var oldPath = Path.Join(_tempDir.FullName, "old.docx");
        var newPath = Path.Join(_tempDir.FullName, "new.docx");
        var id = _sut.AddFile(oldPath);

        _sut.UpdateFile(id, newPath);

        Assert.True(_sut.TryGetPath(id, out var resolved));
        Assert.Equal(newPath, resolved);
    }

    [Fact]
    public void ScanAll_WopiTestFile_GetsWopitestIdentifier()
    {
        var path = Path.Join(_tempDir.FullName, "test.wopitest");
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
        var oldPath = Path.Join(_tempDir.FullName, "old.docx");
        var newPath = Path.Join(_tempDir.FullName, "new.docx");
        var id = _sut.AddFile(oldPath);

        _sut.UpdateFile(id, newPath);

        Assert.False(_sut.TryGetFileId(oldPath, out _));
        Assert.True(_sut.TryGetFileId(newPath, out var resolvedId));
        Assert.Equal(id, resolvedId);
    }

    [Fact]
    public void RemoveId_Path_NoLongerResolvesViaReverseLookup()
    {
        var path = Path.Join(_tempDir.FullName, "doc.docx");
        var id = _sut.AddFile(path);

        _sut.RemoveId(id);

        Assert.False(_sut.TryGetFileId(path, out _));
    }

    [Fact]
    public void ScanAll_Rescan_DropsBindingsForRemovedFiles()
    {
        // Confirm both maps are reset on rescan — clearing only the forward map would let a
        // parallel reverse-map entry linger.
        var stalePath = Path.Join(_tempDir.FullName, "stale.docx");
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
            .Select(i => Path.Join(_tempDir.FullName, $"f{i}.docx"))
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
        var path = Path.Join(_tempDir.FullName, "late.docx");

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
        var oldPath = Path.Join(_tempDir.FullName, "old.docx");
        var newPath = Path.Join(_tempDir.FullName, "new.docx");
        var id = _sut.AddFile(oldPath);
        _sut.UpdateFile(id, newPath);

        Assert.Equal(id, _sut.GetOrAddFileId(newPath));
    }

    [Fact]
    public void AddFile_WopiTestFile_GetsWopitestIdentifier()
    {
        // Id derivation is one policy for every flow — a wopitest file registered through
        // AddFile (create, watcher event, sweep) must get the same fixed id the startup scan
        // assigns, or the validator's configured id would only work for pre-existing files.
        var path = Path.Join(_tempDir.FullName, "test.wopitest");

        var id = _sut.AddFile(path);

        Assert.Equal("WOPITEST", id);
    }

    [Fact]
    public void ScanAll_DotfilePresent_RegistersIt()
    {
        // The tree-walk policy must not skip hidden entries: on Linux, .NET reports leading-dot
        // names as Hidden, and the default EnumerationOptions would silently drop them from the
        // startup scan.
        var path = Path.Join(_tempDir.FullName, ".hidden.docx");
        File.WriteAllText(path, string.Empty);

        _sut.ScanAll(_tempDir.FullName);

        Assert.True(_sut.TryGetFileId(path, out _));
    }

    [Fact]
    public void EnsureMapping_UnmappedPath_RegistersBothDirections()
    {
        var path = Path.Join(_tempDir.FullName, "unseen.docx");
        var id = InMemoryFileIds.DeriveId(path);

        Assert.True(_sut.EnsureMapping(id, path));

        Assert.True(_sut.TryGetPath(id, out var resolved));
        Assert.Equal(path, resolved);
        Assert.True(_sut.TryGetFileId(path, out var reverse));
        Assert.Equal(id, reverse);
        Assert.False(_sut.EnsureMapping(id, path)); // idempotent: nothing to register twice
    }

    [Fact]
    public void EnsureMapping_ReboundPath_KeepsCanonicalReverseBinding()
    {
        // A rename retained old-id for the new path; a peer-derived id for the same path must
        // register as an alias without stealing the path's canonical (retained) id.
        var oldPath = Path.Join(_tempDir.FullName, "old.docx");
        var newPath = Path.Join(_tempDir.FullName, "renamed.docx");
        var retainedId = _sut.AddFile(oldPath);
        _sut.UpdateFile(retainedId, newPath);
        var peerDerivedId = InMemoryFileIds.DeriveId(newPath);

        Assert.True(_sut.EnsureMapping(peerDerivedId, newPath));

        Assert.True(_sut.TryGetPath(peerDerivedId, out var viaAlias));
        Assert.Equal(newPath, viaAlias);
        Assert.True(_sut.TryGetPath(retainedId, out _));            // alias didn't evict the retained id
        Assert.True(_sut.TryGetFileId(newPath, out var canonical));
        Assert.Equal(retainedId, canonical);                        // reverse map still canonical
    }

    [Fact]
    public void TryRemovePath_MappedPath_RemovesBothDirections()
    {
        var path = Path.Join(_tempDir.FullName, "doc.docx");
        var id = _sut.AddFile(path);

        _sut.TryRemovePath(path);

        Assert.False(_sut.TryGetFileId(path, out _));
        Assert.False(_sut.TryGetPath(id, out _));
    }

    [Fact]
    public void TryRemovePath_ReboundId_LeavesNewBindingIntact()
    {
        // The id was rebound to a new path before the old path's removal landed (a delete event
        // arriving late); removing the old path must not tear down the id's new binding.
        var oldPath = Path.Join(_tempDir.FullName, "old.docx");
        var newPath = Path.Join(_tempDir.FullName, "new.docx");
        var id = _sut.AddFile(oldPath);
        _sut.UpdateFile(id, newPath);
        var strayId = InMemoryFileIds.DeriveId(Path.Join(_tempDir.FullName, "stray.docx"));
        _sut.EnsureMapping(strayId, oldPath); // a different id claims the old path

        _sut.TryRemovePath(oldPath);

        Assert.False(_sut.TryGetPath(strayId, out _));
        Assert.True(_sut.TryGetPath(id, out var resolved));
        Assert.Equal(newPath, resolved);
    }

    [Fact]
    public void RepointSubtree_MappedFile_KeepsIdAcrossRename()
    {
        var oldPath = Path.Join(_tempDir.FullName, "old.docx");
        var newPath = Path.Join(_tempDir.FullName, "new.docx");
        var id = _sut.AddFile(oldPath);

        Assert.True(_sut.RepointSubtree(oldPath, newPath));

        Assert.True(_sut.TryGetPath(id, out var resolved));
        Assert.Equal(newPath, resolved);
        Assert.False(_sut.TryGetFileId(oldPath, out _));
        Assert.True(_sut.TryGetFileId(newPath, out var canonical));
        Assert.Equal(id, canonical);
    }

    [Fact]
    public void RepointSubtree_Directory_RepointsChildrenToo()
    {
        // A directory rename changes every child path; each child keeps its id so live sessions
        // on files inside the renamed folder survive.
        var oldDir = Path.Join(_tempDir.FullName, "before");
        var newDir = Path.Join(_tempDir.FullName, "after");
        var childOld = Path.Join(oldDir, "leaf.docx");
        var dirId = _sut.AddFile(oldDir);
        var childId = _sut.AddFile(childOld);

        Assert.True(_sut.RepointSubtree(oldDir, newDir));

        Assert.Equal(newDir, _sut.GetPath(dirId));
        Assert.Equal(Path.Join(newDir, "leaf.docx"), _sut.GetPath(childId));
        Assert.False(_sut.TryGetFileId(childOld, out _));
    }

    [Fact]
    public void RepointSubtree_AliasOnOldPath_TravelsWithoutStealingCanonical()
    {
        // Both the canonical id and an alias point at the old path; after the repoint both must
        // resolve to the new path and the canonical one must stay canonical.
        var oldPath = Path.Join(_tempDir.FullName, "old.docx");
        var newPath = Path.Join(_tempDir.FullName, "new.docx");
        var canonicalId = _sut.AddFile(oldPath);
        var aliasId = InMemoryFileIds.DeriveId(Path.Join(_tempDir.FullName, "elsewhere.docx"));
        _sut.EnsureMapping(aliasId, oldPath);

        Assert.True(_sut.RepointSubtree(oldPath, newPath));

        Assert.Equal(newPath, _sut.GetPath(canonicalId));
        Assert.Equal(newPath, _sut.GetPath(aliasId));
        Assert.True(_sut.TryGetFileId(newPath, out var reverse));
        Assert.Equal(canonicalId, reverse);
    }

    [Fact]
    public void RepointSubtree_UnknownOldPath_ReturnsFalse()
    {
        Assert.False(_sut.RepointSubtree(
            Path.Join(_tempDir.FullName, "never-seen.docx"),
            Path.Join(_tempDir.FullName, "target.docx")));
    }

    [Fact]
    public async Task Mixed_ConcurrentAddAndRemove_LeavesConsistentState()
    {
        // Stress: half the workers add, the other half remove the same file id. Whatever the
        // interleaving, the forward and reverse maps must agree (no dangling reverse entry).
        const int rounds = 200;
        var path = Path.Join(_tempDir.FullName, "shared.docx");

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
