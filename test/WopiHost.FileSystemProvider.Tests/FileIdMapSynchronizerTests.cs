using Microsoft.Extensions.Logging.Abstractions;

namespace WopiHost.FileSystemProvider.Tests;

public class FileIdMapSynchronizerTests : IDisposable
{
    // Generous because watcher delivery runs on OS/threadpool schedules; tests exit as soon as
    // the condition holds, so the full timeout is only ever paid on failure.
    private const int WatcherTimeoutMs = 10_000;

    private readonly InMemoryFileIds _fileIds = new(NullLogger<InMemoryFileIds>.Instance);
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("WopiSyncTest_");
    private readonly List<FileIdMapSynchronizer> _synchronizers = [];

    public void Dispose()
    {
        _synchronizers.ForEach(s => s.Dispose());
        _tempDir.Refresh();
        if (_tempDir.Exists) _tempDir.Delete(recursive: true);
        GC.SuppressFinalize(this);
    }

    private FileIdMapSynchronizer CreateSynchronizer(long reconcileDebounceMs)
    {
        var synchronizer = new FileIdMapSynchronizer(
            _fileIds, _tempDir.FullName, NullLogger.Instance, reconcileDebounceMs);
        _synchronizers.Add(synchronizer);
        return synchronizer;
    }

    private static async Task WaitForAsync(Func<bool> condition, string because)
    {
        var deadline = Environment.TickCount64 + WatcherTimeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return;
            }
            await Task.Delay(25);
        }
        Assert.Fail($"Timed out waiting for {because}.");
    }

    // ---------- Reconciliation sweep ----------

    [Fact]
    public void Reconcile_UnmappedFile_ResolvesAndRegisters()
    {
        // The id another process would derive for a file this map has never seen.
        var path = Path.Join(_tempDir.FullName, "unseen.docx");
        File.WriteAllText(path, string.Empty);
        var peerDerivedId = InMemoryFileIds.DeriveId(path);
        var sut = CreateSynchronizer(reconcileDebounceMs: 0);

        var found = sut.TryResolveByReconcile(peerDerivedId, out var resolved);

        Assert.True(found);
        Assert.Equal(path, resolved);
        Assert.True(_fileIds.TryGetPath(peerDerivedId, out _)); // registered, next lookup is O(1)
    }

    [Fact]
    public void Reconcile_ReboundPath_ResolvesAliasWithoutStealingCanonical()
    {
        // A rename retained old-id for the new path; a peer-derived id for the same path must
        // resolve as an alias without stealing the path's canonical (retained) id.
        var oldPath = Path.Join(_tempDir.FullName, "old.docx");
        var newPath = Path.Join(_tempDir.FullName, "renamed.docx");
        File.WriteAllText(newPath, string.Empty);
        var retainedId = _fileIds.AddFile(oldPath);
        _fileIds.UpdateFile(retainedId, newPath);
        var peerDerivedId = InMemoryFileIds.DeriveId(newPath);
        var sut = CreateSynchronizer(reconcileDebounceMs: 0);

        Assert.True(sut.TryResolveByReconcile(peerDerivedId, out var resolved));

        Assert.Equal(newPath, resolved);
        Assert.True(_fileIds.TryGetPath(retainedId, out _));
        Assert.True(_fileIds.TryGetFileId(newPath, out var canonical));
        Assert.Equal(retainedId, canonical);
    }

    [Fact]
    public void Reconcile_MalformedId_ReturnsFalseWithoutSweeping()
    {
        // No tree entry can ever hash to a malformed id, so it must be rejected before any
        // disk access — proven here by the debounce window staying unconsumed for the
        // legitimate resolve that follows.
        var path = Path.Join(_tempDir.FullName, "real.docx");
        File.WriteAllText(path, string.Empty);
        var sut = CreateSynchronizer(reconcileDebounceMs: long.MaxValue / 2);

        Assert.False(sut.TryResolveByReconcile("not-a-real-id", out _));
        Assert.True(sut.TryResolveByReconcile(InMemoryFileIds.DeriveId(path), out var resolved));
        Assert.Equal(path, resolved);
    }

    [Fact]
    public void Reconcile_SweepRegistersEveryEntry_SoMissesWithinDebounceStillResolve()
    {
        // One sweep registers the whole tree. An unresolvable (but well-formed) id consumes the
        // debounce window, yet a legitimate id arriving inside the window resolves from the map
        // the sweep just populated — the failure mode where a garbage id 404s a real file is gone.
        var path = Path.Join(_tempDir.FullName, "legit.docx");
        File.WriteAllText(path, string.Empty);
        var unresolvableId = new string('a', 64);
        var sut = CreateSynchronizer(reconcileDebounceMs: long.MaxValue / 2);

        Assert.False(sut.TryResolveByReconcile(unresolvableId, out _));
        Assert.True(sut.TryResolveByReconcile(InMemoryFileIds.DeriveId(path), out var resolved));
        Assert.Equal(path, resolved);
    }

    [Fact]
    public void Reconcile_NewFileWithinDebounceWindow_ReportsMiss()
    {
        // A file that appeared after the last sweep stays unresolved for the rest of the window
        // (the watcher and the client's retry cover that race); once the window elapses the next
        // miss sweeps again.
        var sut = CreateSynchronizer(reconcileDebounceMs: long.MaxValue / 2);
        Assert.False(sut.TryResolveByReconcile(new string('a', 64), out _)); // consumes the window

        var path = Path.Join(_tempDir.FullName, "late.docx");
        File.WriteAllText(path, string.Empty);

        Assert.False(sut.TryResolveByReconcile(InMemoryFileIds.DeriveId(path), out _));

        var immediate = CreateSynchronizer(reconcileDebounceMs: 0);
        Assert.True(immediate.TryResolveByReconcile(InMemoryFileIds.DeriveId(path), out _));
    }

    [Fact]
    public void Reconcile_WopiTestFile_ResolvesFixedId()
    {
        var path = Path.Join(_tempDir.FullName, "test.wopitest");
        File.WriteAllText(path, string.Empty);
        var sut = CreateSynchronizer(reconcileDebounceMs: 0);

        Assert.True(sut.TryResolveByReconcile("WOPITEST", out var resolved));
        Assert.Equal(path, resolved);
    }

    [Fact]
    public void Reconcile_MissingRoot_ReportsMissWithoutThrowing()
    {
        var missingRoot = Path.Join(_tempDir.FullName, "gone");
        var sut = new FileIdMapSynchronizer(_fileIds, missingRoot, NullLogger.Instance, reconcileDebounceMs: 0);
        _synchronizers.Add(sut);

        Assert.False(sut.TryResolveByReconcile(new string('a', 64), out _));
    }

    // ---------- Watcher ----------

    [Fact]
    public async Task Watcher_FileCreated_RegistersId()
    {
        var sut = CreateSynchronizer(reconcileDebounceMs: long.MaxValue / 2);
        sut.StartWatching();
        Assert.True(sut.IsWatching);

        var path = Path.Join(_tempDir.FullName, "created.docx");
        File.WriteAllText(path, string.Empty);

        await WaitForAsync(() => _fileIds.TryGetFileId(path, out _), "the created file to be registered");
    }

    [Fact]
    public async Task Watcher_FileDeleted_RemovesBinding()
    {
        var path = Path.Join(_tempDir.FullName, "doomed.docx");
        File.WriteAllText(path, string.Empty);
        _fileIds.ScanAll(_tempDir.FullName);
        var sut = CreateSynchronizer(reconcileDebounceMs: long.MaxValue / 2);
        sut.StartWatching();

        File.Delete(path);

        await WaitForAsync(() => !_fileIds.TryGetFileId(path, out _), "the deleted file's binding to be removed");
    }

    [Fact]
    public async Task Watcher_FileRenamed_RepointsSameId()
    {
        // The heart of the design: the rename event carries both paths, so the existing id is
        // repointed instead of a second id being minted — the lock domain stays unified.
        var oldPath = Path.Join(_tempDir.FullName, "before.docx");
        var newPath = Path.Join(_tempDir.FullName, "after.docx");
        File.WriteAllText(oldPath, string.Empty);
        _fileIds.ScanAll(_tempDir.FullName);
        Assert.True(_fileIds.TryGetFileId(oldPath, out var originalId));
        var sut = CreateSynchronizer(reconcileDebounceMs: long.MaxValue / 2);
        sut.StartWatching();

        File.Move(oldPath, newPath);

        await WaitForAsync(
            () => _fileIds.TryGetFileId(newPath, out var id) && id == originalId,
            "the rename to repoint the original id");
        Assert.False(_fileIds.TryGetFileId(oldPath, out _));
        Assert.Equal(newPath, _fileIds.GetPath(originalId));
    }

    [Fact]
    public async Task Watcher_DirectoryRenamed_RepointsChildIds()
    {
        // One Renamed event arrives for the directory only; the map must carry the children
        // across so a session on a file inside the renamed folder keeps its id.
        var oldDir = _tempDir.CreateSubdirectory("team-a");
        var childOld = Path.Join(oldDir.FullName, "notes.docx");
        File.WriteAllText(childOld, string.Empty);
        _fileIds.ScanAll(_tempDir.FullName);
        Assert.True(_fileIds.TryGetFileId(childOld, out var childId));
        var sut = CreateSynchronizer(reconcileDebounceMs: long.MaxValue / 2);
        sut.StartWatching();

        var newDir = Path.Join(_tempDir.FullName, "team-b");
        Directory.Move(oldDir.FullName, newDir);

        var childNew = Path.Join(newDir, "notes.docx");
        await WaitForAsync(
            () => _fileIds.TryGetFileId(childNew, out var id) && id == childId,
            "the directory rename to repoint the child id");
    }

    [Fact]
    public async Task Watcher_RenameOfUnmappedPath_RegistersNewPath()
    {
        // Safe-save pattern: an unmapped temp file is renamed onto the target name. There is no
        // id to repoint, so the new path registers like a create.
        var sut = CreateSynchronizer(reconcileDebounceMs: long.MaxValue / 2);
        sut.StartWatching();
        var tempPath = Path.Join(_tempDir.FullName, "save.tmp");
        var finalPath = Path.Join(_tempDir.FullName, "document.docx");
        File.WriteAllText(tempPath, string.Empty);
        await WaitForAsync(() => _fileIds.TryGetFileId(tempPath, out _), "the temp file to be registered");
        _fileIds.TryRemovePath(tempPath); // simulate a map that missed the create

        File.Move(tempPath, finalPath);

        await WaitForAsync(() => _fileIds.TryGetFileId(finalPath, out _), "the rename target to be registered");
    }

    [Fact]
    public void StartWatching_MissingRoot_DegradesWithoutThrowing()
    {
        var missingRoot = Path.Join(_tempDir.FullName, "gone");
        var sut = new FileIdMapSynchronizer(_fileIds, missingRoot, NullLogger.Instance);
        _synchronizers.Add(sut);

        sut.StartWatching();

        Assert.False(sut.IsWatching);
    }

    [Fact]
    public void Dispose_StopsWatchingAndIsIdempotent()
    {
        var sut = CreateSynchronizer(reconcileDebounceMs: 0);
        sut.StartWatching();
        Assert.True(sut.IsWatching);

        sut.Dispose();
        sut.Dispose();

        Assert.False(sut.IsWatching);
    }
}
