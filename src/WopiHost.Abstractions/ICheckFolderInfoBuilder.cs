using Microsoft.AspNetCore.Http;

namespace WopiHost.Abstractions;

/// <summary>
/// Builds the <see cref="WopiCheckFolderInfo"/> response for a single folder. Replace the
/// default registration in DI to take full control of the response shape.
/// </summary>
/// <remarks>
/// <para>
/// Synchronous on purpose — see #363. The host-customization hook
/// <see cref="IWopiHostExtensions.OnCheckFolderInfoAsync"/> is fired by the controller after
/// the builder returns, so the controller's only <c>await</c> on this path is the direct hook
/// invocation. An async builder would re-introduce the Infer# null-deref FP that the issue
/// tracks. Replacements should preserve the sync shape.
/// </para>
/// <para>
/// Unlike the file and container builders, this contract does <em>not</em> invoke the
/// extension hook itself — the controller does. Implementations only need to construct the
/// default response.
/// </para>
/// </remarks>
public interface ICheckFolderInfoBuilder
{
    /// <summary>
    /// Builds the default <see cref="WopiCheckFolderInfo"/> for <paramref name="folder"/>.
    /// </summary>
    /// <param name="folder">The folder the response describes.</param>
    /// <param name="httpContext">The current request context. Used for the authenticated principal.</param>
    WopiCheckFolderInfo Build(IWopiFolder folder, HttpContext httpContext);
}
