using Microsoft.AspNetCore.Http;

namespace WopiHost.Abstractions;

/// <summary>
/// Builds the <see cref="WopiCheckContainerInfo"/> response for a single container. Replace
/// the default registration in DI to take full control of the response shape.
/// </summary>
/// <remarks>
/// The default implementation that ships with <c>WopiHost.Core</c> populates the response from
/// the container's metadata and the registered <see cref="IWopiPermissionProvider"/>, then
/// invokes <see cref="IWopiHostExtensions.OnCheckContainerInfoAsync"/> for last-mile host
/// customization. Replacements are responsible for any of those steps they want to preserve,
/// including the final extension-hook invocation.
/// </remarks>
public interface ICheckContainerInfoBuilder
{
    /// <summary>
    /// Builds a fully populated <see cref="WopiCheckContainerInfo"/> for <paramref name="container"/>.
    /// </summary>
    /// <param name="container">The container the response describes.</param>
    /// <param name="httpContext">The current request context. Used for the authenticated principal
    /// and access to per-request services.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WopiCheckContainerInfo> BuildAsync(
        IWopiFolder container,
        HttpContext httpContext,
        CancellationToken cancellationToken = default);
}
