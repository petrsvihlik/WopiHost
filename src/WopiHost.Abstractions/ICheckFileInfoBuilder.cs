namespace WopiHost.Abstractions;

/// <summary>
/// Builds the <see cref="WopiCheckFileInfo"/> response for a single file. Replace the default
/// registration in DI to take full control of the response shape (e.g. populate custom
/// properties on a derived <see cref="WopiCheckFileInfo"/>, set a CDN-backed
/// <c>FileUrl</c>, or short-circuit the permission resolution).
/// </summary>
/// <remarks>
/// <para>
/// The default implementation that ships with <c>WopiHost.Core</c> populates the response from
/// the file's metadata, host capabilities, user claims and the registered
/// <see cref="IWopiPermissionProvider"/>, then invokes
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
    /// <param name="request">The framework-neutral request envelope. Carries the authenticated
    /// principal, the proxy-aware request URL, the resolved access token, request headers, and
    /// the per-request service scope; the default builder reads only the principal.</param>
    /// <param name="capabilities">The host capability flags (lock, Cobalt, co-authoring support)
    /// to copy onto the response, or <see langword="null"/> to keep the response defaults.</param>
    /// <param name="userInfo">The user's previously stored <c>UserInfo</c> value (see WOPI
    /// <c>PutUserInfo</c>), or <see langword="null"/> when none is stored.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The <c>CheckFileInfo</c> response to serialize to the WOPI client.</returns>
    Task<WopiCheckFileInfo> BuildAsync(
        IWopiFile file,
        WopiRequestInfo request,
        WopiHostCapabilities? capabilities = null,
        string? userInfo = null,
        CancellationToken cancellationToken = default);
}
