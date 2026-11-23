using System.Security.Claims;
using Cobalt;

namespace WopiHost.Cobalt;

/// <summary>
/// Provides the Cobalt host locking store for a single WOPI file, reporting schema locks and
/// co-authoring state from a shared <see cref="CoauthoringSessionTracker"/>.
/// </summary>
/// <remarks>
/// The caller's identity is read from <see cref="CurrentPrincipal"/>, which <see cref="CobaltProcessor"/>
/// sets for the duration of each request. Exclusive-lock, metadata, rename, delete, and version
/// requests are accepted with empty default outputs.
/// </remarks>
/// <param name="fileId">The identifier of the WOPI file this store serves.</param>
/// <param name="sessionTracker">The tracker that records the file's active editors.</param>
public class CobaltHostLockingStore(
    string fileId,
    CoauthoringSessionTracker sessionTracker) : HostLockingStore
{
    /// <summary>
    /// Holds the principal of the request currently executing. <see cref="CobaltProcessor"/> caches one
    /// <see cref="CobaltHostLockingStore"/> per file (it lives inside the cached
    /// <c>CobaltFile</c>) so the locking store cannot capture a single principal
    /// at construction; instead each <see cref="CobaltProcessor.ProcessCobalt"/> invocation sets this
    /// <see cref="AsyncLocal{T}"/> before the request runs and restores the previous value after.
    /// </summary>
    internal static readonly AsyncLocal<ClaimsPrincipal?> CurrentPrincipal = new();

    private readonly string _fileId = fileId;
    private readonly CoauthoringSessionTracker _sessionTracker = sessionTracker;

    private static ClaimsPrincipal? Principal => CurrentPrincipal.Value;
    private string UserId => Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    private string UserName => Principal?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

    /// <inheritdoc/>
    public override WhoAmIRequest.OutputType HandleWhoAmI(WhoAmIRequest.InputType input)
    {
        var p = Principal;
        var login = p?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return new WhoAmIRequest.OutputType
        {
            UserEmailAddress = p?.FindFirst(ClaimTypes.Email)?.Value,
            UserIsAnonymous = string.IsNullOrEmpty(login),
            UserLogin = login,
            UserName = p?.FindFirst(ClaimTypes.Name)?.Value,
        };
    }

    /// <inheritdoc/>
    public override ServerTimeRequest.OutputType HandleServerTime(ServerTimeRequest.InputType input)
    {
        var result = new ServerTimeRequest.OutputType { ServerTime = DateTime.UtcNow };

        return result;
    }

    /// <inheritdoc/>
    public override LockStatusRequest.OutputType HandleLockStatus(LockStatusRequest.InputType input)
    {
        var result = new LockStatusRequest.OutputType
        {
            LockType = LockType.SchemaLock
        };

        return result;
    }

    /// <inheritdoc/>
    public override GetExclusiveLockRequest.OutputType HandleGetExclusiveLock(GetExclusiveLockRequest.InputType input)
    {
        var result = new GetExclusiveLockRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override RefreshExclusiveLockRequest.OutputType HandleRefreshExclusiveLock(RefreshExclusiveLockRequest.InputType input)
    {
        var result = new RefreshExclusiveLockRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override CheckExclusiveLockAvailabilityRequest.OutputType HandleCheckExclusiveLockAvailability(CheckExclusiveLockAvailabilityRequest.InputType input)
    {
        var result = new CheckExclusiveLockAvailabilityRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override ConvertExclusiveLockToSchemaLockRequest.OutputType HandleConvertExclusiveLockToSchemaLock(ConvertExclusiveLockToSchemaLockRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        var result = new ConvertExclusiveLockToSchemaLockRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override ConvertExclusiveLockWithCoauthTransitionRequest.OutputType HandleConvertExclusiveLockWithCoauthTransition(ConvertExclusiveLockWithCoauthTransitionRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        var result = new ConvertExclusiveLockWithCoauthTransitionRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override GetSchemaLockRequest.OutputType HandleGetSchemaLock(GetSchemaLockRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        var result = new GetSchemaLockRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override ReleaseExclusiveLockRequest.OutputType HandleReleaseExclusiveLock(ReleaseExclusiveLockRequest.InputType input)
    {
        var result = new ReleaseExclusiveLockRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override ReleaseSchemaLockRequest.OutputType HandleReleaseSchemaLock(ReleaseSchemaLockRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        var result = new ReleaseSchemaLockRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override RefreshSchemaLockRequest.OutputType HandleRefreshSchemaLock(RefreshSchemaLockRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        var result = new RefreshSchemaLockRequest.OutputType { Lock = LockType.SchemaLock };

        return result;
    }

    /// <inheritdoc/>
    public override ConvertSchemaLockToExclusiveLockRequest.OutputType HandleConvertSchemaLockToExclusiveLock(ConvertSchemaLockToExclusiveLockRequest.InputType input)
    {
        var result = new ConvertSchemaLockToExclusiveLockRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override CheckSchemaLockAvailabilityRequest.OutputType HandleCheckSchemaLockAvailability(CheckSchemaLockAvailabilityRequest.InputType input)
    {
        var result = new CheckSchemaLockAvailabilityRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override ExitCoauthoringRequest.OutputType HandleExitCoauthoring(ExitCoauthoringRequest.InputType input, int protocolMajorVersion, int protocolMinorVersion)
    {
        _sessionTracker.RemoveSession(_fileId, UserId);
        var result = new ExitCoauthoringRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public override ConvertCoauthLockToExclusiveLockRequest.OutputType HandleConvertCoauthLockToExclusiveLock(ConvertCoauthLockToExclusiveLockRequest.InputType input)
    {
        var result = new ConvertCoauthLockToExclusiveLockRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override CheckCoauthLockAvailabilityRequest.OutputType HandleCheckCoauthLockAvailability(CheckCoauthLockAvailabilityRequest.InputType input)
    {
        var result = new CheckCoauthLockAvailabilityRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override MarkCoauthTransitionCompleteRequest.OutputType HandleMarkCoauthTransitionComplete(MarkCoauthTransitionCompleteRequest.InputType input)
    {
        var result = new MarkCoauthTransitionCompleteRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override GetCoauthoringStatusRequest.OutputType HandleGetCoauthoringStatus(GetCoauthoringStatusRequest.InputType input)
    {
        var result = new GetCoauthoringStatusRequest.OutputType
        {
            CoauthStatus = _sessionTracker.GetCoauthStatus(_fileId, UserId)
        };

        return result;
    }

    /// <inheritdoc/>
    public override EditorsTable QueryEditorsTable() =>
        _sessionTracker.GetEditorsTable(_fileId);

    /// <inheritdoc/>
    public override JoinEditingSessionRequest.OutputType HandleJoinEditingSession(JoinEditingSessionRequest.InputType input)
    {
        _sessionTracker.AddOrRefreshSession(_fileId, UserId, UserName);
        var result = new JoinEditingSessionRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override RefreshEditingSessionRequest.OutputType HandleRefreshEditingSession(RefreshEditingSessionRequest.InputType input)
    {
        _sessionTracker.AddOrRefreshSession(_fileId, UserId, UserName);
        var result = new RefreshEditingSessionRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override LeaveEditingSessionRequest.OutputType HandleLeaveEditingSession(LeaveEditingSessionRequest.InputType input)
    {
        _sessionTracker.RemoveSession(_fileId, UserId);
        var result = new LeaveEditingSessionRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override UpdateEditorMetadataRequest.OutputType HandleUpdateEditorMetadata(UpdateEditorMetadataRequest.InputType input)
    {
        var result = new UpdateEditorMetadataRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override RemoveEditorMetadataRequest.OutputType HandleRemoveEditorMetadata(RemoveEditorMetadataRequest.InputType input)
    {
        var result = new RemoveEditorMetadataRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override ulong GetEditorsTableWaterline() => 0;

    /// <inheritdoc/>
    public override AmIAloneRequest.OutputType HandleAmIAlone(AmIAloneRequest.InputType input)
    {
        var result = new AmIAloneRequest.OutputType { AmIAlone = _sessionTracker.IsAlone(_fileId, UserId) };

        return result;
    }

    /// <inheritdoc/>
    public override DocMetaInfoRequest.OutputType HandleDocMetaInfo(DocMetaInfoRequest.InputType input)
    {
        var result = new DocMetaInfoRequest.OutputType();

        return result;
    }

    /// <inheritdoc/>
    public override VersionsRequest.OutputType HandleVersions(VersionsRequest.InputType input)
    {
        var result = new VersionsRequest.OutputType { Enabled = false };

        return result;
    }

    // The methods below cover protocol features WopiHost doesn't implement yet (rename,
    // delete, version history, editors-property metadata). Stubbed with
    // empty default outputs so the host accepts the request without erroring;
    // hosts that need the real behavior should override these.

    /// <inheritdoc/>
    public override EnumerateEditorsRequest.OutputType HandleEnumerateEditors(EnumerateEditorsRequest.InputType input) =>
        new();

    /// <inheritdoc/>
    public override EditorsPropertyCheckRequest.OutputType HandleEditorsPropertyCheck(EditorsPropertyCheckRequest.InputType input) =>
        new();

    /// <inheritdoc/>
    public override RenameFileRequest.OutputType HandleRename(RenameFileRequest.InputType input) =>
        new();

    /// <inheritdoc/>
    public override DeleteFileRequest.OutputType HandleDelete(DeleteFileRequest.InputType input) =>
        new();

    /// <inheritdoc/>
    public override GetVersionListRequest.OutputType HandleGetVersionList(GetVersionListRequest.InputType input) =>
        new();

    /// <inheritdoc/>
    public override RestoreVersionRequest.OutputType HandleRestoreVersion(RestoreVersionRequest.InputType input) =>
        new();

    // Indicates whether the file backing this locking store still exists.
    // WopiHost only constructs CobaltHostLockingStore for an active WOPI session,
    // which by definition has a backing file — so `true` is the correct answer
    // here. If a host wants to surface deletes from a side channel it can override.
    /// <inheritdoc/>
    public override bool FileExists() => true;
}
