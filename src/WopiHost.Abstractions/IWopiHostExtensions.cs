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
    /// set <see cref="WopiCheckFileInfo.FileUrl"/>, override capability flags reported to the
    /// WOPI client.
    /// </remarks>
    /// <example>
    /// Advertising a <see cref="WopiCheckFileInfo.FileUrl"/>. Per the WOPI proof-keys spec,
    /// clients fetch <c>FileUrl</c> <b>without</b> proof signing, so it must point at a location
    /// served outside the proof-validated <c>/wopi</c> surface — a CDN, a pre-signed blob URL,
    /// or a host-mapped download route that checks the access token but no proof headers.
    /// In an ASP.NET Core host, <c>LinkGenerator</c> builds the URL from such a named route:
    /// <code>
    /// // Composition root: an unsigned, token-checked download route OUTSIDE /wopi.
    /// // app.MapGet("/download/{id}", DownloadHandler).WithName("FileDownload");
    /// public sealed class FileUrlExtensions(IHttpContextAccessor httpContextAccessor) : WopiHostExtensions
    /// {
    ///     public override Task&lt;WopiCheckFileInfo&gt; OnCheckFileInfoAsync(
    ///         WopiCheckFileInfoContext context, CancellationToken cancellationToken = default)
    ///     {
    ///         var httpContext = httpContextAccessor.HttpContext!;
    ///         var links = httpContext.RequestServices.GetRequiredService&lt;LinkGenerator&gt;();
    ///         var url = links.GetUriByName(httpContext, "FileDownload", new
    ///         {
    ///             id = context.File.Identifier,
    ///             access_token = httpContext.Request.GetAccessToken(),
    ///         });
    ///         context.CheckFileInfo.FileUrl = url is null ? null : new Uri(url);
    ///         return Task.FromResult(context.CheckFileInfo);
    ///     }
    /// }
    /// </code>
    /// A mirror of this implementation is compiled in <c>WopiHost.Core.Tests</c>
    /// (<c>DefaultCheckInfoBuilderTests.FileUrlExtensions</c>) so the pattern cannot silently
    /// stop building; keep the two in sync.
    /// </example>
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

    /// <summary>
    /// Hook raised when a WOPI client reports activities (comments / mentions) on a file via
    /// <c>AddActivities</c>. Use this to send notifications, surface an activity feed, or prompt to
    /// share with a mentioned user. The core decides each activity's protocol response status; this
    /// hook is a side-effect seam. The default is a no-op.
    /// </summary>
    Task OnAddActivitiesAsync(WopiAddActivitiesContext context, CancellationToken cancellationToken = default);
}
