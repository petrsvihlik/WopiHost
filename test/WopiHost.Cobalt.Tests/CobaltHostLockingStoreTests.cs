using System.Security.Claims;
using Cobalt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace WopiHost.Cobalt.Tests;

/// <summary>
/// Behavior tests for <see cref="CobaltHostLockingStore"/>'s coauth/editing-session handlers
/// — the bridge between Cobalt's HostLockingStore contract and
/// <see cref="CoauthoringSessionTracker"/>. The stub handlers (e.g. <c>HandleGetSchemaLock</c>)
/// that just return a default output have no behavior worth verifying; the tests cover the ones
/// that actually delegate to the tracker or read principal claims.
/// </summary>
public class CobaltHostLockingStoreTests
{
    private static string NewFileId() => $"file-lock-{Guid.NewGuid():N}";

    /// <summary>
    /// Builds a fresh tracker + locking-store pair for each test, and sets
    /// <see cref="CobaltHostLockingStore.CurrentPrincipal"/> for the duration of an action.
    /// The principal is per-test via AsyncLocal, so tests can run in parallel safely.
    /// </summary>
    private static (CobaltHostLockingStore Store, CoauthoringSessionTracker Tracker, string FileId) Build()
    {
        var fileId = NewFileId();
        var tracker = new CoauthoringSessionTracker(NullLogger<CoauthoringSessionTracker>.Instance);
        var store = new CobaltHostLockingStore(fileId, tracker);
        return (store, tracker, fileId);
    }

    private static ClaimsPrincipal Principal(string userId, string? userName = null, string? email = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        if (userName is not null) claims.Add(new(ClaimTypes.Name, userName));
        if (email is not null) claims.Add(new(ClaimTypes.Email, email));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static T WithPrincipal<T>(ClaimsPrincipal principal, Func<T> action)
    {
        var prev = CobaltHostLockingStore.CurrentPrincipal.Value;
        CobaltHostLockingStore.CurrentPrincipal.Value = principal;
        try { return action(); }
        finally { CobaltHostLockingStore.CurrentPrincipal.Value = prev!; }
    }

    [Fact]
    public void HandleWhoAmI_ProjectsClaimsOnto_OutputType()
    {
        var (store, _, _) = Build();
        var principal = Principal("user-1", userName: "Alice", email: "alice@example.com");

        var result = WithPrincipal(principal, () => store.HandleWhoAmI(new WhoAmIRequest.InputType()));

        Assert.Equal("user-1", result.UserLogin);
        Assert.Equal("Alice", result.UserName);
        Assert.Equal("alice@example.com", result.UserEmailAddress);
        Assert.False(result.UserIsAnonymous);
    }

    [Fact]
    public void HandleWhoAmI_NoPrincipal_ReportsAnonymous()
    {
        var (store, _, _) = Build();

        // No principal set in AsyncLocal — UserLogin is empty so UserIsAnonymous = true.
        var result = WithPrincipal(null!, () => store.HandleWhoAmI(new WhoAmIRequest.InputType()));

        Assert.True(result.UserIsAnonymous);
        Assert.Null(result.UserLogin);
    }

    [Fact]
    public void HandleJoinCoauthoring_RegistersSessionAndReportsAlone()
    {
        var (store, tracker, fileId) = Build();
        var principal = Principal("user-1", userName: "Alice");

        var result = WithPrincipal(principal, () => store.HandleJoinCoauthoring(null!, 0, 0));

        Assert.Equal(LockType.SchemaLock, result.Lock);
        Assert.Equal(CoauthStatusType.Alone, result.CoauthStatus);
        Assert.NotEqual(Guid.Empty, (Guid)result.TransitionId);
        Assert.Equal(1, tracker.GetActiveEditorCount(fileId));
    }

    [Fact]
    public void HandleJoinCoauthoring_SecondUser_ReportsCoauthoring()
    {
        var (store, tracker, fileId) = Build();

        WithPrincipal(Principal("user-1", userName: "Alice"), () => store.HandleJoinCoauthoring(null!, 0, 0));
        var result = WithPrincipal(Principal("user-2", userName: "Bob"),
            () => store.HandleJoinCoauthoring(null!, 0, 0));

        Assert.Equal(CoauthStatusType.Coauthoring, result.CoauthStatus);
        Assert.Equal(2, tracker.GetActiveEditorCount(fileId));
    }

    [Fact]
    public void HandleExitCoauthoring_RemovesSession()
    {
        var (store, tracker, fileId) = Build();
        var principal = Principal("user-1", userName: "Alice");

        WithPrincipal(principal, () => store.HandleJoinCoauthoring(null!, 0, 0));
        Assert.Equal(1, tracker.GetActiveEditorCount(fileId));

        WithPrincipal(principal, () => store.HandleExitCoauthoring(null!, 0, 0));

        Assert.Equal(0, tracker.GetActiveEditorCount(fileId));
    }

    [Fact]
    public void HandleRefreshCoauthoring_KeepsSessionActive()
    {
        var (store, tracker, fileId) = Build();
        var principal = Principal("user-1", userName: "Alice");

        WithPrincipal(principal, () => store.HandleJoinCoauthoring(null!, 0, 0));
        var result = WithPrincipal(principal, () => store.HandleRefreshCoauthoring(null!, 0, 0));

        Assert.Equal(LockType.SchemaLock, result.Lock);
        Assert.Equal(CoauthStatusType.Alone, result.CoauthStatus);
        Assert.Equal(1, tracker.GetActiveEditorCount(fileId));
    }

    [Fact]
    public void HandleAmIAlone_AfterJoin_True()
    {
        var (store, _, _) = Build();
        var principal = Principal("user-1", userName: "Alice");

        WithPrincipal(principal, () => store.HandleJoinCoauthoring(null!, 0, 0));
        var result = WithPrincipal(principal, () => store.HandleAmIAlone(new AmIAloneRequest.InputType()));

        Assert.True(result.AmIAlone);
    }

    [Fact]
    public void HandleAmIAlone_TwoUsers_False()
    {
        var (store, _, _) = Build();
        WithPrincipal(Principal("user-1", userName: "Alice"), () => store.HandleJoinCoauthoring(null!, 0, 0));
        WithPrincipal(Principal("user-2", userName: "Bob"), () => store.HandleJoinCoauthoring(null!, 0, 0));

        var resultA = WithPrincipal(Principal("user-1"), () => store.HandleAmIAlone(new AmIAloneRequest.InputType()));
        var resultB = WithPrincipal(Principal("user-2"), () => store.HandleAmIAlone(new AmIAloneRequest.InputType()));

        Assert.False(resultA.AmIAlone);
        Assert.False(resultB.AmIAlone);
    }

    [Fact]
    public void HandleGetCoauthoringStatus_ReflectsTrackerState()
    {
        var (store, _, _) = Build();
        WithPrincipal(Principal("user-1"), () => store.HandleJoinCoauthoring(null!, 0, 0));
        WithPrincipal(Principal("user-2"), () => store.HandleJoinCoauthoring(null!, 0, 0));

        var result = WithPrincipal(Principal("user-1"),
            () => store.HandleGetCoauthoringStatus(new GetCoauthoringStatusRequest.InputType()));

        Assert.Equal(CoauthStatusType.Coauthoring, result.CoauthStatus);
    }

    [Fact]
    public void QueryEditorsTable_DelegatesToTracker()
    {
        var (store, _, _) = Build();
        WithPrincipal(Principal("user-1", userName: "Alice"), () => store.HandleJoinCoauthoring(null!, 0, 0));
        WithPrincipal(Principal("user-2", userName: "Bob"), () => store.HandleJoinCoauthoring(null!, 0, 0));

        var table = store.QueryEditorsTable();

        Assert.Equal(2, table.Count);
    }

    [Fact]
    public void HandleJoinEditingSession_RegistersSession()
    {
        var (store, tracker, fileId) = Build();
        WithPrincipal(Principal("user-1", userName: "Alice"),
            () => store.HandleJoinEditingSession(new JoinEditingSessionRequest.InputType()));

        Assert.Equal(1, tracker.GetActiveEditorCount(fileId));
    }

    [Fact]
    public void HandleRefreshEditingSession_KeepsSession()
    {
        var (store, tracker, fileId) = Build();
        WithPrincipal(Principal("user-1", userName: "Alice"),
            () => store.HandleJoinEditingSession(new JoinEditingSessionRequest.InputType()));
        WithPrincipal(Principal("user-1", userName: "Alice"),
            () => store.HandleRefreshEditingSession(new RefreshEditingSessionRequest.InputType()));

        Assert.Equal(1, tracker.GetActiveEditorCount(fileId));
    }

    [Fact]
    public void HandleLeaveEditingSession_RemovesSession()
    {
        var (store, tracker, fileId) = Build();
        WithPrincipal(Principal("user-1", userName: "Alice"),
            () => store.HandleJoinEditingSession(new JoinEditingSessionRequest.InputType()));
        WithPrincipal(Principal("user-1"),
            () => store.HandleLeaveEditingSession(new LeaveEditingSessionRequest.InputType()));

        Assert.Equal(0, tracker.GetActiveEditorCount(fileId));
    }

    [Fact]
    public void HandleServerTime_IsCloseToNow()
    {
        var (store, _, _) = Build();
        var before = DateTime.UtcNow;
        var result = store.HandleServerTime(new ServerTimeRequest.InputType());
        var after = DateTime.UtcNow;

        Assert.InRange(result.ServerTime, before, after);
    }

    [Fact]
    public void HandleLockStatus_DefaultsToSchemaLock()
    {
        var (store, _, _) = Build();
        var result = store.HandleLockStatus(new LockStatusRequest.InputType());
        Assert.Equal(LockType.SchemaLock, result.LockType);
    }

    [Fact]
    public void HandleRefreshSchemaLock_ReportsSchemaLock()
    {
        var (store, _, _) = Build();
        var result = store.HandleRefreshSchemaLock(null!, 0, 0);
        Assert.Equal(LockType.SchemaLock, result.Lock);
    }

    [Fact]
    public void GetEditorsTableWaterline_IsZero()
    {
        var (store, _, _) = Build();
        Assert.Equal(0ul, store.GetEditorsTableWaterline());
    }

    [Fact]
    public void FileExists_AlwaysTrue()
    {
        var (store, _, _) = Build();
        Assert.True(store.FileExists());
    }

    [Fact]
    public void HandleVersions_ReportsDisabled()
    {
        var (store, _, _) = Build();
        var result = store.HandleVersions(new VersionsRequest.InputType());
        Assert.False(result.Enabled);
    }

    // The handlers below are intentional pass-throughs that return a default OutputType.
    // They cover protocol features WopiHost doesn't implement (exclusive locks, version
    // history, editor metadata, rename/delete via Cobalt instead of WOPI). The contract
    // is just "don't throw" — which is what these tests verify. They are kept as separate
    // facts so a regression on any one handler is precise in the test report.

    [Fact]
    public void HandleGetExclusiveLock_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleGetExclusiveLock(new GetExclusiveLockRequest.InputType()));

    [Fact]
    public void HandleRefreshExclusiveLock_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleRefreshExclusiveLock(new RefreshExclusiveLockRequest.InputType()));

    [Fact]
    public void HandleCheckExclusiveLockAvailability_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleCheckExclusiveLockAvailability(new CheckExclusiveLockAvailabilityRequest.InputType()));

    [Fact]
    public void HandleConvertExclusiveLockToSchemaLock_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleConvertExclusiveLockToSchemaLock(null!, 0, 0));

    [Fact]
    public void HandleConvertExclusiveLockWithCoauthTransition_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleConvertExclusiveLockWithCoauthTransition(null!, 0, 0));

    [Fact]
    public void HandleGetSchemaLock_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleGetSchemaLock(null!, 0, 0));

    [Fact]
    public void HandleReleaseExclusiveLock_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleReleaseExclusiveLock(new ReleaseExclusiveLockRequest.InputType()));

    [Fact]
    public void HandleReleaseSchemaLock_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleReleaseSchemaLock(null!, 0, 0));

    [Fact]
    public void HandleConvertSchemaLockToExclusiveLock_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleConvertSchemaLockToExclusiveLock(new ConvertSchemaLockToExclusiveLockRequest.InputType()));

    [Fact]
    public void HandleCheckSchemaLockAvailability_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleCheckSchemaLockAvailability(new CheckSchemaLockAvailabilityRequest.InputType()));

    [Fact]
    public void HandleConvertCoauthLockToExclusiveLock_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleConvertCoauthLockToExclusiveLock(new ConvertCoauthLockToExclusiveLockRequest.InputType()));

    [Fact]
    public void HandleCheckCoauthLockAvailability_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleCheckCoauthLockAvailability(new CheckCoauthLockAvailabilityRequest.InputType()));

    [Fact]
    public void HandleMarkCoauthTransitionComplete_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleMarkCoauthTransitionComplete(new MarkCoauthTransitionCompleteRequest.InputType()));

    [Fact]
    public void HandleUpdateEditorMetadata_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleUpdateEditorMetadata(new UpdateEditorMetadataRequest.InputType()));

    [Fact]
    public void HandleRemoveEditorMetadata_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleRemoveEditorMetadata(new RemoveEditorMetadataRequest.InputType()));

    [Fact]
    public void HandleDocMetaInfo_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleDocMetaInfo(new DocMetaInfoRequest.InputType()));

    [Fact]
    public void HandleEnumerateEditors_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleEnumerateEditors(new EnumerateEditorsRequest.InputType()));

    [Fact]
    public void HandleEditorsPropertyCheck_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleEditorsPropertyCheck(new EditorsPropertyCheckRequest.InputType()));

    [Fact]
    public void HandleRename_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleRename(new RenameFileRequest.InputType()));

    [Fact]
    public void HandleDelete_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleDelete(new DeleteFileRequest.InputType()));

    [Fact]
    public void HandleGetVersionList_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleGetVersionList(new GetVersionListRequest.InputType()));

    [Fact]
    public void HandleRestoreVersion_DoesNotThrow()
        => Assert.NotNull(Build().Store.HandleRestoreVersion(new RestoreVersionRequest.InputType()));
}
