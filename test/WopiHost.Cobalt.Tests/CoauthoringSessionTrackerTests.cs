using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace WopiHost.Cobalt.Tests;

public class CoauthoringSessionTrackerTests
{
    private readonly CoauthoringSessionTracker _tracker = new(NullLogger<CoauthoringSessionTracker>.Instance);

    private static string NewFileId() => $"file-{Guid.NewGuid():N}";

    [Fact]
    public void GetActiveEditorCount_NoSessions_ReturnsZero()
    {
        Assert.Equal(0, _tracker.GetActiveEditorCount(NewFileId()));
    }

    [Fact]
    public void IsAlone_NoSessions_ReturnsTrue()
    {
        Assert.True(_tracker.IsAlone(NewFileId(), "any-user"));
    }

    [Fact]
    public void GetCoauthStatus_NoSessions_ReturnsAlone()
    {
        Assert.Equal(global::Cobalt.CoauthStatusType.Alone, _tracker.GetCoauthStatus(NewFileId(), "any-user"));
    }

    [Fact]
    public void GetEditorsTable_NoSessions_ReturnsEmpty()
    {
        Assert.Empty(_tracker.GetEditorsTable(NewFileId()));
    }

    [Fact]
    public void AddOrRefreshSession_RegistersOneEditor()
    {
        var fileId = NewFileId();
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");

        Assert.Equal(1, _tracker.GetActiveEditorCount(fileId));
        Assert.True(_tracker.IsAlone(fileId, "user-1"));
        Assert.Equal(global::Cobalt.CoauthStatusType.Alone, _tracker.GetCoauthStatus(fileId, "user-1"));
        Assert.Single(_tracker.GetEditorsTable(fileId));
    }

    [Fact]
    public void AddOrRefreshSession_SameUserTwice_DeduplicatesByUserId()
    {
        // Multiple browser tabs from the same user must collapse into a single editor —
        // the bug #139 was reporting duplicate user names in OOS.
        var fileId = NewFileId();
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");

        Assert.Equal(1, _tracker.GetActiveEditorCount(fileId));
        Assert.Single(_tracker.GetEditorsTable(fileId));
    }

    [Fact]
    public void AddOrRefreshSession_TwoUsers_BothActive()
    {
        var fileId = NewFileId();
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");
        _tracker.AddOrRefreshSession(fileId, "user-2", "Bob");

        Assert.Equal(2, _tracker.GetActiveEditorCount(fileId));
        Assert.False(_tracker.IsAlone(fileId, "user-1"));
        Assert.False(_tracker.IsAlone(fileId, "user-2"));
        Assert.Equal(global::Cobalt.CoauthStatusType.Coauthoring, _tracker.GetCoauthStatus(fileId, "user-1"));
        Assert.Equal(2, _tracker.GetEditorsTable(fileId).Count);
    }

    [Fact]
    public void RemoveSession_LastEditor_TableIsEmpty()
    {
        var fileId = NewFileId();
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");
        _tracker.RemoveSession(fileId, "user-1");

        Assert.Equal(0, _tracker.GetActiveEditorCount(fileId));
        Assert.Empty(_tracker.GetEditorsTable(fileId));
        Assert.True(_tracker.IsAlone(fileId, "user-1"));
    }

    [Fact]
    public void RemoveSession_OneOfTwo_RemainingUserIsAlone()
    {
        var fileId = NewFileId();
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");
        _tracker.AddOrRefreshSession(fileId, "user-2", "Bob");
        _tracker.RemoveSession(fileId, "user-2");

        Assert.Equal(1, _tracker.GetActiveEditorCount(fileId));
        Assert.True(_tracker.IsAlone(fileId, "user-1"));
        Assert.Equal(global::Cobalt.CoauthStatusType.Alone, _tracker.GetCoauthStatus(fileId, "user-1"));
    }

    [Fact]
    public void RemoveSession_UnknownUser_NoOp()
    {
        var fileId = NewFileId();
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");
        _tracker.RemoveSession(fileId, "stranger");

        Assert.Equal(1, _tracker.GetActiveEditorCount(fileId));
    }

    [Fact]
    public void RemoveSession_UnknownFile_NoOp()
    {
        // Must not throw or pollute state for a fileId that was never registered.
        _tracker.RemoveSession(NewFileId(), "any-user");
    }

    [Fact]
    public void Sessions_AreIsolatedPerFile()
    {
        var fileA = NewFileId();
        var fileB = NewFileId();
        _tracker.AddOrRefreshSession(fileA, "user-1", "Alice");
        _tracker.AddOrRefreshSession(fileB, "user-2", "Bob");

        Assert.Equal(1, _tracker.GetActiveEditorCount(fileA));
        Assert.Equal(1, _tracker.GetActiveEditorCount(fileB));
        Assert.True(_tracker.IsAlone(fileA, "user-1"));
        Assert.True(_tracker.IsAlone(fileB, "user-2"));
    }

    [Fact]
    public void GetEditorsTable_PopulatesUserMetadata()
    {
        var fileId = NewFileId();
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");
        _tracker.AddOrRefreshSession(fileId, "user-2", "Bob");

        var table = _tracker.GetEditorsTable(fileId);
        Assert.Equal(2, table.Count);

        var entries = table.Values
            .OfType<global::Cobalt.EditorsTableEntryNew>()
            .OrderBy(e => e.UserName, StringComparer.Ordinal)
            .ToList();
        Assert.Equal("Alice", entries[0].UserName);
        Assert.Equal("user-1", entries[0].UserLogin);
        Assert.True(entries[0].HasEditPermission);
        Assert.Equal("Bob", entries[1].UserName);
        Assert.Equal("user-2", entries[1].UserLogin);
    }

    [Fact]
    public void GetEditorsTable_UsesStableClientIdPerUser()
    {
        // Same user across two refreshes (same fileId) must hash to the same ArrayGuid
        // so the editors-table key collapses — this is what enforces the dedup-by-user
        // behavior that the older string-keyed table provided.
        var fileId = NewFileId();
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");
        var firstKey = _tracker.GetEditorsTable(fileId).Keys.Single();
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");
        var secondKey = _tracker.GetEditorsTable(fileId).Keys.Single();

        Assert.Equal(firstKey, secondKey);
    }

    [Fact]
    public void GetEditorsTable_DifferentUsers_DifferentClientIds()
    {
        var fileId = NewFileId();
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");
        _tracker.AddOrRefreshSession(fileId, "user-2", "Bob");

        var keys = _tracker.GetEditorsTable(fileId).Keys.ToList();
        Assert.Equal(2, keys.Count);
        Assert.NotEqual(keys[0], keys[1]);
    }

    [Fact]
    public void IsAlone_UserNotInSessions_StillTrueWhenOthersPresent()
    {
        // A user that hasn't joined yet should be considered "alone" only when there are
        // no OTHER editors. With another user already in the session, IsAlone is false
        // for the absent user too.
        var fileId = NewFileId();
        _tracker.AddOrRefreshSession(fileId, "user-1", "Alice");

        Assert.False(_tracker.IsAlone(fileId, "stranger"));
    }
}
