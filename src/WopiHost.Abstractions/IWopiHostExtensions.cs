namespace WopiHost.Abstractions;

/// <summary>
/// Provides host-customization hooks for WOPI request handling: audit, telemetry, and final
/// response mutations without replacing whole builders.
/// </summary>
/// <remarks>
/// <para>
/// Each method has a no-op default in the <see cref="WopiHostExtensions"/> base class; hosts
/// derive from it and override only the hooks they need. A single implementation is registered
/// in DI as <see cref="IWopiHostExtensions"/> and resolved by both the default builders
/// (<see cref="ICheckFileInfoBuilder"/>, <see cref="ICheckContainerInfoBuilder"/>) and the
/// endpoints (for the folder, ecosystem, put-file, and activities hooks).
/// </para>
/// <para>
/// Throwing inside any callback turns the response into a <c>500</c>. For best-effort
/// bookkeeping (audit log, last-edit telemetry), swallow exceptions inside the handler.
/// </para>
/// </remarks>
public interface IWopiHostExtensions
{
    /// <summary>
    /// Customizes the <c>CheckFileInfo</c> response after the default builder has populated it
    /// from the file's metadata, host capabilities, and the principal's permissions.
    /// </summary>
    /// <remarks>
    /// Typical uses: copy custom properties onto a derived <see cref="WopiCheckFileInfo"/>,
    /// set <see cref="WopiCheckFileInfo.FileUrl"/>, override capability flags reported to the
    /// WOPI client. For a worked <see cref="WopiCheckFileInfo.FileUrl"/> example (and the
    /// proof-keys constraint that makes it subtle), see the wiki:
    /// <see href="https://github.com/petrsvihlik/WopiHost/wiki/CheckFileInfo-Customization#setting-fileurl"/>.
    /// </remarks>
    /// <param name="context">The default response plus the file and principal it was built for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The (possibly modified) response to serialize to the WOPI client.</returns>
    Task<WopiCheckFileInfo> OnCheckFileInfoAsync(WopiCheckFileInfoContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Customizes the <c>CheckContainerInfo</c> response. Mirrors
    /// <see cref="OnCheckFileInfoAsync"/> for containers.
    /// </summary>
    /// <param name="context">The default response plus the container and principal it was built for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The (possibly modified) response to serialize to the WOPI client.</returns>
    Task<WopiCheckContainerInfo> OnCheckContainerInfoAsync(WopiCheckContainerInfoContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Customizes the <c>CheckFolderInfo</c> response (a OneNote for the web operation). Mirrors
    /// <see cref="OnCheckFileInfoAsync"/> for folders.
    /// </summary>
    /// <param name="context">The default response plus the folder and principal it was built for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The (possibly modified) response to serialize to the WOPI client.</returns>
    Task<WopiCheckFolderInfo> OnCheckFolderInfoAsync(WopiCheckFolderInfoContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Customizes the <c>CheckEcosystem</c> response.
    /// </summary>
    /// <remarks>
    /// The spec strongly recommends that <see cref="WopiCheckEcosystem.SupportsContainers"/>
    /// match the value reported by <c>CheckFileInfo</c>; a host that overrides that flag in
    /// <see cref="OnCheckFileInfoAsync"/> should mirror the change here.
    /// </remarks>
    /// <param name="context">The default response plus the requesting principal.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The (possibly modified) response to serialize to the WOPI client.</returns>
    Task<WopiCheckEcosystem> OnCheckEcosystemAsync(WopiCheckEcosystemContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs after a successful <c>PutFile</c> write, for audit trails and last-touch metadata.
    /// </summary>
    /// <param name="context">The updated file plus the user ids parsed from the optional
    /// <c>X-WOPI-Editors</c> request header.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnPutFileAsync(WopiPutFileContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs after a successful <c>PutRelativeFile</c> write, for example to flag
    /// conversion-context uploads or record declared-size telemetry.
    /// </summary>
    /// <param name="context">The original and newly created files plus the optional
    /// <c>X-WOPI-FileConversion</c> (presence flag) and <c>X-WOPI-Size</c> header values.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnPutRelativeFileAsync(WopiPutRelativeFileContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs when a WOPI client reports activities (comments, mentions) on a file via
    /// <c>AddActivities</c>, for example to send notifications or feed an activity stream.
    /// </summary>
    /// <remarks>
    /// The hook is side-effect only: <c>WopiHost.Core</c> decides each activity's protocol
    /// response status.
    /// </remarks>
    /// <param name="context">The file, the requesting principal, and the reported activities.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnAddActivitiesAsync(WopiAddActivitiesContext context, CancellationToken cancellationToken = default);
}
