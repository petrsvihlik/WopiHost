using System.Security.Claims;
using Cobalt;

namespace WopiHost.Cobalt;

public class CobaltHostLockingStore(
    ClaimsPrincipal principal,
    string fileId,
    CoauthoringSessionTracker sessionTracker) : HostLockingStore
{
    private readonly ClaimsPrincipal _principal = principal;
    private readonly string _fileId = fileId;
    private readonly CoauthoringSessionTracker _sessionTracker = sessionTracker;

    private string UserId => _principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    private string UserName => _principal?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

    public override WhoAmIRequest.OutputType HandleWhoAmI(WhoAmIRequest.InputType input)
    {
        var result = new WhoAmIRequest.OutputType
        {
            UserEmailAddress = _principal?.FindFirst(ClaimTypes.Email).Value,
            UserIsAnonymous = string.IsNullOrEmpty(_principal?.FindFirst(ClaimTypes.NameIdentifier).Value),
            UserLogin = _principal?.FindFirst(ClaimTypes.NameIdentifier).Value,
            UserName = _principal?.FindFirst(ClaimTypes.Name).Value
        };

        return result;
    }

    public override ServerTimeRequest.OutputType HandleServerTime(ServerTimeRequest.InputType input)
    {
        var result = new ServerTimeRequest.OutputType { ServerTime = DateTime.UtcNow };

        return result;
    }

    public override LockStatusRequest.OutputType HandleLockStatus(LockStatusRequest.InputType input)
    {
        // Replaces the 15.x `HandleLockAndCheckOutStatus`. The output shape
        // changed too: old (LockType, CheckOutType) → new (LockType, LockId, LockedBy).
        var result = new LockStatusRequest.OutputType
        {
            LockType = LockType.SchemaLock
        };

        return result;
    }

    public override GetExclusiveLockRequest.OutputType HandleGetExclusiveLock(GetExclusiveLockRequest.InputType input)
    {
        var result = new GetExclusiveLockRequest.OutputType();

        return result;
    }

    public override RefreshExclusiveLockRequest.OutputType HandleRefreshExclusiveLock(RefreshExclusiveLockRequest.InputType input)
    {
        var result = new RefreshExclusiveLockRequest.OutputType();

        return result;
    }

    public override CheckExclusiveLockAvailabilityRequest.OutputType HandleCheckExclusiveLockAvailability(CheckExclusiveLockAvailabilityRequest.InputType input)
    {
        var result = new CheckExclusiveLockAvailabilityRequest.OutputType();

        return result;
    }

    public override ConvertExclusiveLockToSchemaLockRequest.OutputType HandleConvertExclusiveLockToSchemaLock(ConvertExclusiveLockToSchemaLockRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        var result = new ConvertExclusiveLockToSchemaLockRequest.OutputType();

        return result;
    }

    public override ConvertExclusiveLockWithCoauthTransitionRequest.OutputType HandleConvertExclusiveLockWithCoauthTransition(ConvertExclusiveLockWithCoauthTransitionRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        var result = new ConvertExclusiveLockWithCoauthTransitionRequest.OutputType();

        return result;
    }

    public override GetSchemaLockRequest.OutputType HandleGetSchemaLock(GetSchemaLockRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        var result = new GetSchemaLockRequest.OutputType();

        return result;
    }

    public override ReleaseExclusiveLockRequest.OutputType HandleReleaseExclusiveLock(ReleaseExclusiveLockRequest.InputType input)
    {
        var result = new ReleaseExclusiveLockRequest.OutputType();

        return result;
    }

    public override ReleaseSchemaLockRequest.OutputType HandleReleaseSchemaLock(ReleaseSchemaLockRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        var result = new ReleaseSchemaLockRequest.OutputType();

        return result;
    }

    public override RefreshSchemaLockRequest.OutputType HandleRefreshSchemaLock(RefreshSchemaLockRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        var result = new RefreshSchemaLockRequest.OutputType { Lock = LockType.SchemaLock };

        return result;
    }

    public override ConvertSchemaLockToExclusiveLockRequest.OutputType HandleConvertSchemaLockToExclusiveLock(ConvertSchemaLockToExclusiveLockRequest.InputType input)
    {
        var result = new ConvertSchemaLockToExclusiveLockRequest.OutputType();

        return result;
    }

    public override CheckSchemaLockAvailabilityRequest.OutputType HandleCheckSchemaLockAvailability(CheckSchemaLockAvailabilityRequest.InputType input)
    {
        var result = new CheckSchemaLockAvailabilityRequest.OutputType();

        return result;
    }

    public override JoinCoauthoringRequest.OutputType HandleJoinCoauthoring(JoinCoauthoringRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        _sessionTracker.AddOrRefreshSession(_fileId, UserId, UserName);
        var result = new JoinCoauthoringRequest.OutputType
        {
            Lock = LockType.SchemaLock,
            CoauthStatus = _sessionTracker.GetCoauthStatus(_fileId, UserId),
            TransitionId = Guid.NewGuid()
        };
        return result;
    }

    public override ExitCoauthoringRequest.OutputType HandleExitCoauthoring(ExitCoauthoringRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        _sessionTracker.RemoveSession(_fileId, UserId);
        var result = new ExitCoauthoringRequest.OutputType();

        return result;
    }

    public override RefreshCoauthoringSessionRequest.OutputType HandleRefreshCoauthoring(RefreshCoauthoringSessionRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        _sessionTracker.AddOrRefreshSession(_fileId, UserId, UserName);
        var result = new RefreshCoauthoringSessionRequest.OutputType
        {
            Lock = LockType.SchemaLock,
            CoauthStatus = _sessionTracker.GetCoauthStatus(_fileId, UserId)
        };

        return result;
    }

    public override ConvertCoauthLockToExclusiveLockRequest.OutputType HandleConvertCoauthLockToExclusiveLock(ConvertCoauthLockToExclusiveLockRequest.InputType input)
    {
        var result = new ConvertCoauthLockToExclusiveLockRequest.OutputType();

        return result;
    }

    public override CheckCoauthLockAvailabilityRequest.OutputType HandleCheckCoauthLockAvailability(CheckCoauthLockAvailabilityRequest.InputType input)
    {
        var result = new CheckCoauthLockAvailabilityRequest.OutputType();

        return result;
    }

    public override MarkCoauthTransitionCompleteRequest.OutputType HandleMarkCoauthTransitionComplete(MarkCoauthTransitionCompleteRequest.InputType input)
    {
        var result = new MarkCoauthTransitionCompleteRequest.OutputType();

        return result;
    }

    public override GetCoauthoringStatusRequest.OutputType HandleGetCoauthoringStatus(GetCoauthoringStatusRequest.InputType input)
    {
        var result = new GetCoauthoringStatusRequest.OutputType
        {
            CoauthStatus = _sessionTracker.GetCoauthStatus(_fileId, UserId)
        };

        return result;
    }

    public override EditorsTable QueryEditorsTable() =>
        _sessionTracker.GetEditorsTable(_fileId);

    public override JoinEditingSessionRequest.OutputType HandleJoinEditingSession(JoinEditingSessionRequest.InputType input)
    {
        _sessionTracker.AddOrRefreshSession(_fileId, UserId, UserName);
        var result = new JoinEditingSessionRequest.OutputType();

        return result;
    }

    public override RefreshEditingSessionRequest.OutputType HandleRefreshEditingSession(RefreshEditingSessionRequest.InputType input)
    {
        _sessionTracker.AddOrRefreshSession(_fileId, UserId, UserName);
        var result = new RefreshEditingSessionRequest.OutputType();

        return result;
    }

    public override LeaveEditingSessionRequest.OutputType HandleLeaveEditingSession(LeaveEditingSessionRequest.InputType input)
    {
        _sessionTracker.RemoveSession(_fileId, UserId);
        var result = new LeaveEditingSessionRequest.OutputType();

        return result;
    }

    public override UpdateEditorMetadataRequest.OutputType HandleUpdateEditorMetadata(UpdateEditorMetadataRequest.InputType input)
    {
        var result = new UpdateEditorMetadataRequest.OutputType();

        return result;
    }

    public override RemoveEditorMetadataRequest.OutputType HandleRemoveEditorMetadata(RemoveEditorMetadataRequest.InputType input)
    {
        var result = new RemoveEditorMetadataRequest.OutputType();

        return result;
    }

    public override ulong GetEditorsTableWaterline() => 0;

    public override AmIAloneRequest.OutputType HandleAmIAlone(AmIAloneRequest.InputType input)
    {
        var result = new AmIAloneRequest.OutputType { AmIAlone = _sessionTracker.IsAlone(_fileId, UserId) };

        return result;
    }

    public override DocMetaInfoRequest.OutputType HandleDocMetaInfo(DocMetaInfoRequest.InputType input)
    {
        var result = new DocMetaInfoRequest.OutputType();

        return result;
    }

    public override VersionsRequest.OutputType HandleVersions(VersionsRequest.InputType input)
    {
        var result = new VersionsRequest.OutputType { Enabled = false };

        return result;
    }

    // The methods below were added to HostLockingStore in CobaltCore 16.x.
    // They cover protocol features WopiHost doesn't implement yet (rename,
    // delete, version history, editors-property metadata). Stubbed with
    // empty default outputs so the host accepts the request without erroring;
    // hosts that need the real behavior should override these.

    public override EnumerateEditorsRequest.OutputType HandleEnumerateEditors(EnumerateEditorsRequest.InputType input) =>
        new();

    public override EditorsPropertyCheckRequest.OutputType HandleEditorsPropertyCheck(EditorsPropertyCheckRequest.InputType input) =>
        new();

    public override RenameFileRequest.OutputType HandleRename(RenameFileRequest.InputType input) =>
        new();

    public override DeleteFileRequest.OutputType HandleDelete(DeleteFileRequest.InputType input) =>
        new();

    public override GetVersionListRequest.OutputType HandleGetVersionList(GetVersionListRequest.InputType input) =>
        new();

    public override RestoreVersionRequest.OutputType HandleRestoreVersion(RestoreVersionRequest.InputType input) =>
        new();

    // Indicates whether the file backing this locking store still exists.
    // WopiHost only constructs CobaltHostLockingStore for an active WOPI session,
    // which by definition has a backing file — so `true` is the correct answer
    // here. If a host wants to surface deletes from a side channel it can override.
    public override bool FileExists() => true;
}
