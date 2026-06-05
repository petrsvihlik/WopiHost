namespace WopiHost.Abstractions;

/// <summary>
/// Host-customization seam for WOPI request handling. Hosts plug in audit, telemetry, and
/// final response mutations here without replacing whole builders.
/// </summary>
/// <remarks>
/// <para>
/// Each method has a no-op default in <see cref="WopiHostExtensions"/> (the supplied base class).
/// Customers extend that base and override only the methods they care about. A single
/// implementation is registered in DI as <see cref="IWopiHostExtensions"/> and resolved by both
/// the default builders (<see cref="ICheckFileInfoBuilder"/>,
/// <see cref="ICheckContainerInfoBuilder"/>) and the controllers (for the Put-file hooks,
/// the folder hook, and the ecosystem hook).
/// </para>
/// <para>
/// Throwing inside any callback turns the response into a <c>500</c>. For best-effort
/// bookkeeping (audit log, last-edit telemetry), swallow exceptions inside the handler.
/// </para>
/// </remarks>
public interface IWopiHostExtensions
{
    /// <summary>
    /// Last-mile customization hook for <c>CheckFileInfo</c>. Receives the default response
    /// built from the file's metadata, host capabilities, and the principal's permissions;
    /// returns the (possibly modified) response that gets serialized to the client.
    /// </summary>
    /// <remarks>
    /// Typical uses: copy custom properties onto a derived <see cref="WopiCheckFileInfo"/>,
    /// rewrite <c>FileUrl</c> to point at a CDN, override capability flags reported to the
    /// WOPI client.
    /// </remarks>
    Task<WopiCheckFileInfo> OnCheckFileInfoAsync(WopiCheckFileInfoContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Last-mile customization hook for <c>CheckContainerInfo</c>. Mirrors
    /// <see cref="OnCheckFileInfoAsync"/> for containers.
    /// </summary>
    Task<WopiCheckContainerInfo> OnCheckContainerInfoAsync(WopiCheckContainerInfoContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Last-mile customization hook for the OneNote-for-the-web <c>CheckFolderInfo</c>
    /// operation. Mirrors <see cref="OnCheckFileInfoAsync"/> for folders.
    /// </summary>
    Task<WopiCheckFolderInfo> OnCheckFolderInfoAsync(WopiCheckFolderInfoContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Last-mile customization hook for <c>CheckEcosystem</c>. Mirrors the spec's strong
    /// recommendation that <see cref="WopiCheckEcosystem.SupportsContainers"/> match the value
    /// reported by <c>CheckFileInfo</c>; hosts that override that flag in
    /// <see cref="OnCheckFileInfoAsync"/> should mirror the change here.
    /// </summary>
    Task<WopiCheckEcosystem> OnCheckEcosystemAsync(WopiCheckEcosystemContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hook raised after a successful <c>PutFile</c> write. Receives the updated file plus the
    /// user ids parsed from the optional <c>X-WOPI-Editors</c> request header. Use this for
    /// audit trails and last-touch metadata. The default is a no-op.
    /// </summary>
    Task OnPutFileAsync(WopiPutFileContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hook raised after a successful <c>PutRelativeFile</c> write. Surfaces the optional
    /// <c>X-WOPI-FileConversion</c> (presence flag) and <c>X-WOPI-Size</c> headers alongside
    /// both the original and newly-created files. Hosts that want to flag conversion-context
    /// uploads or record declared-size telemetry plug in here; the default is a no-op.
    /// </summary>
    Task OnPutRelativeFileAsync(WopiPutRelativeFileContext context, CancellationToken cancellationToken = default);
}
