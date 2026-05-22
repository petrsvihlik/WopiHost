namespace WopiHost.Abstractions;

/// <summary>
/// Builds the <see cref="WopiCheckFileInfo"/> response for a single file. Replace the default
/// registration in DI to take full control of the response shape (e.g. populate custom
/// properties on a derived <see cref="WopiCheckFileInfo"/>, swap in a CDN-backed
/// <c>FileUrl</c>, or short-circuit the permission resolution).
/// </summary>
/// <remarks>
/// <para>
/// The default implementation that ships with <c>WopiHost.Core</c> populates the response from
/// the file's metadata, host capabilities, user claims, the registered
/// <see cref="IWopiPermissionProvider"/>, and the resolved access token, then invokes
/// <see cref="IWopiHostExtensions.OnCheckFileInfoAsync"/> for last-mile host customization.
/// Replacements are responsible for any of those steps they want to preserve, including the
/// final extension-hook invocation.
/// </para>
/// <para>
/// This is the recommended seam when the per-request customization in
/// <see cref="IWopiHostExtensions"/> is not enough — for example, when the host needs to
/// inject scoped services that don't fit in <see cref="WopiCheckFileInfoContext"/>.
/// </para>
/// </remarks>
public interface ICheckFileInfoBuilder
{
    /// <summary>
    /// Builds a fully populated <see cref="WopiCheckFileInfo"/> for <paramref name="file"/>.
    /// </summary>
    /// <param name="file">The file the response describes.</param>
    /// <param name="request">Framework-neutral request envelope — carries the authenticated
    /// principal, the proxy-aware request URL (for <c>FileUrl</c> construction), the resolved
    /// access token, request headers, and the per-request service scope.</param>
    /// <param name="capabilities">Per-controller capability flags (lock/cobalt/coauth support)
    /// to copy onto the response. <see langword="null"/> leaves the response defaults.</param>
    /// <param name="userInfo">Cached value of the user's previously-stored
    /// <c>UserInfo</c> blob (see WOPI <c>PutUserInfo</c>). <see langword="null"/> when unset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WopiCheckFileInfo> BuildAsync(
        IWopiFile file,
        WopiRequestInfo request,
        WopiHostCapabilities? capabilities = null,
        string? userInfo = null,
        CancellationToken cancellationToken = default);
}
