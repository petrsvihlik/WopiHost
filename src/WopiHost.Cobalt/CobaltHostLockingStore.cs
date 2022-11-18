using System.Security.Claims;
using Cobalt;

namespace WopiHost.Cobalt;

public class CobaltHostLockingStore : HostLockingStore
{
    private readonly ClaimsPrincipal _principal;

    public CobaltHostLockingStore(ClaimsPrincipal principal)
    {
        _principal = principal;
    }

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

    public override LockAndCheckOutStatusRequest.OutputType HandleLockAndCheckOutStatus(LockAndCheckOutStatusRequest.InputType input)
    {
        var result = new LockAndCheckOutStatusRequest.OutputType
        {
            LockType = 1U,
            CheckOutType = 0U
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
        var result = new JoinCoauthoringRequest.OutputType
        {
            Lock = LockType.SchemaLock,
            CoauthStatus = CoauthStatusType.Alone,
            TransitionId = Guid.NewGuid()
        };
        return result;
    }

    public override ExitCoauthoringRequest.OutputType HandleExitCoauthoring(ExitCoauthoringRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        var result = new ExitCoauthoringRequest.OutputType();

        return result;
    }

    public override RefreshCoauthoringSessionRequest.OutputType HandleRefreshCoauthoring(RefreshCoauthoringSessionRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        var result = new RefreshCoauthoringSessionRequest.OutputType
        {
            Lock = LockType.SchemaLock,
            CoauthStatus = CoauthStatusType.Alone
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
            CoauthStatus = CoauthStatusType.Alone
        };

        return result;
    }

    public override Dictionary<string, EditorsTableEntry> QueryEditorsTable()
    {
        return new Dictionary<string, EditorsTableEntry>();
    }

    public override JoinEditingSessionRequest.OutputType HandleJoinEditingSession(JoinEditingSessionRequest.InputType input)
    {
        var result = new JoinEditingSessionRequest.OutputType();

        return result;
    }

    public override RefreshEditingSessionRequest.OutputType HandleRefreshEditingSession(RefreshEditingSessionRequest.InputType input)
    {
        var result = new RefreshEditingSessionRequest.OutputType();

        return result;
    }

    public override LeaveEditingSessionRequest.OutputType HandleLeaveEditingSession(LeaveEditingSessionRequest.InputType input)
    {
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

    public override ulong GetEditorsTableWaterline()
    {
        return 0;
    }

    public override AmIAloneRequest.OutputType HandleAmIAlone(AmIAloneRequest.InputType input)
    {
        var result = new AmIAloneRequest.OutputType { AmIAlone = true };

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
}
