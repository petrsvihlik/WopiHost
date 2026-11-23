using Microsoft.Extensions.Logging.Abstractions;

namespace WopiHost.FileSystemProvider.Tests;

public class FileIdMapSynchronizerTests : IDisposable
{
    // Generous because watcher delivery runs on OS/threadpool schedules; tests exit as soon as
    // the condition holds, so the full timeout is only ever paid on failure.
    private const int WatcherTimeoutMs = 10_000;
    private const int MaxRenameAttempts = 5;

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

    /// <summary>
    /// Moves <paramref name="source"/> to <paramref name="target"/> and waits for the synchronizer
    /// to apply the resulting event.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the target kept the source's id (a delivered Renamed event);
    /// <see langword="false"/> if the OS delivered the move as Deleted + Created, so the target
    /// carries its path-derived id instead.
    /// </returns>
    /// <remarks>
    /// On Linux the runtime pairs <c>IN_MOVED_FROM</c> with <c>IN_MOVED_TO</c> only when the two are consecutive
    /// in the inotify queue and the second arrives within a 2 ms poll. The kernel does not enqueue
    /// the pair atomically, and every watcher in the process shares one inotify instance, so a
    /// concurrent test's file activity or a loaded runner can split a rename. The product treats a
    /// split rename as a lost event (a documented degradation), so only a delivered Renamed is a
    /// valid oracle for repointing.
    /// </remarks>
    private async Task<bool> MoveAndAwaitEventAsync(string source, string target, Action<string, string> move)
    {
        Assert.True(_fileIds.TryGetFileId(source, out var sourceId));
        move(source, target);
        string? targetId = null;
        await WaitForAsync(() => _fileIds.TryGetFileId(target, out targetId), "the move's watcher event to be applied");
        return targetId == sourceId;
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

    [Theory]
    [InlineData("not-a-real-id")] // wrong length
    [InlineData("WOPITEST-ish")]  // wrong length, WOPITEST prefix
    public void Reconcile_MalformedId_ReturnsFalseWithoutSweeping(string malformedId)
    {
        // No tree entry can ever hash to a malformed id, so it must be rejected before any
        // disk access — proven here by the debounce window staying unconsumed for the
        // legitimate resolve that follows.
        var path = Path.Join(_tempDir.FullName, "real.docx");
        File.WriteAllText(path, string.Empty);
        var sut = CreateSynchronizer(reconcileDebounceMs: long.MaxValue / 2);

        Assert.False(sut.TryResolveByReconcile(malformedId, out _));
        Assert.False(sut.TryResolveByReconcile(new string('A', 64), out _)); // right length, not lower-hex
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
        var sut = CreateSynchronizer(reconcileDebounceMs: long.MaxValue / 2);
        sut.StartWatching();

        for (var attempt = 0; attempt < MaxRenameAttempts; attempt++)
        {
            var oldPath = Path.Join(_tempDir.FullName, $"before-{attempt}.docx");
            var newPath = Path.Join(_tempDir.FullName, $"after-{attempt}.docx");
            File.WriteAllText(oldPath, string.Empty);
            _fileIds.ScanAll(_tempDir.FullName);
            Assert.True(_fileIds.TryGetFileId(oldPath, out var originalId));

            if (await MoveAndAwaitEventAsync(oldPath, newPath, File.Move))
            {
                Assert.False(_fileIds.TryGetFileId(oldPath, out _));
                Assert.Equal(newPath, _fileIds.GetPath(originalId));
                return;
            }
        }
        Assert.Fail($"The OS split all {MaxRenameAttempts} renames into Deleted + Created.");
    }

    [Fact]
    public async Task Watcher_DirectoryRenamed_RepointsChildIds()
    {
        // One Renamed event arrives for the directory only; the map must carry the children
        // across so a session on a file inside the renamed folder keeps its id.
        var sut = CreateSynchronizer(reconcileDebounceMs: long.MaxValue / 2);
        sut.StartWatching();

        for (var attempt = 0; attempt < MaxRenameAttempts; attempt++)
        {
            var oldDir = _tempDir.CreateSubdirectory($"team-a-{attempt}");
            var childOld = Path.Join(oldDir.FullName, "notes.docx");
            File.WriteAllText(childOld, string.Empty);
            _fileIds.ScanAll(_tempDir.FullName);
            Assert.True(_fileIds.TryGetFileId(childOld, out var childId));
            var newDir = Path.Join(_tempDir.FullName, $"team-b-{attempt}");

            if (await MoveAndAwaitEventAsync(oldDir.FullName, newDir, Directory.Move))
            {
                Assert.True(_fileIds.TryGetFileId(Path.Join(newDir, "notes.docx"), out var childNewId));
                Assert.Equal(childId, childNewId);
                return;
            }
        }
        Assert.Fail($"The OS split all {MaxRenameAttempts} renames into Deleted + Created.");
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
    public void ApplyEvent_UnappliableEvent_IsSwallowed()
    {
        // Watcher callbacks run on threadpool threads where an escaping exception is fatal to
        // the process; an event the map can't apply (here: a name no path API accepts) must be
        // logged and dropped. Synthetic event — a real watcher never gets far enough to raise it.
        var sut = CreateSynchronizer(reconcileDebounceMs: 0);

        sut.ApplyEvent(new FileSystemEventArgs(WatcherChangeTypes.Created, _tempDir.FullName, "bad\0name.docx"));

        Assert.False(_fileIds.WasScanned); // nothing was registered, nothing threw
    }

    [Fact]
    public async Task ScheduleRecoverySweep_RegistersEntriesMissedByLostEvents()
    {
        // The watcher's Error path: events were dropped, so the sweep must pick up whatever
        // appeared unnoticed. Driven directly — a buffer overflow can't be provoked on demand.
        var path = Path.Join(_tempDir.FullName, "missed.docx");
        File.WriteAllText(path, string.Empty);
        var sut = CreateSynchronizer(reconcileDebounceMs: 0);

        sut.ScheduleRecoverySweep();

        await WaitForAsync(() => _fileIds.TryGetFileId(path, out _), "the recovery sweep to register the file");
    }

    [Fact]
    public void StartWatching_MissingRoot_DegradesWithoutThrowing()
    {
        var missingRoot = Path.Join(_tempDir.FullName, "gone");
        using var sut = new FileIdMapSynchronizer(_fileIds, missingRoot, NullLogger.Instance);

        sut.StartWatching();

        Assert.False(sut.IsWatching);
    }

    [Fact]
    public void Dispose_StopsWatchingAndIsIdempotent()
    {
        using var sut = new FileIdMapSynchronizer(_fileIds, _tempDir.FullName, NullLogger.Instance);
        sut.StartWatching();
        Assert.True(sut.IsWatching);

        sut.Dispose();
        sut.Dispose();

        Assert.False(sut.IsWatching);
    }
}
