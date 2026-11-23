using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Builds the <see cref="WopiCheckFolderInfo"/> response for a single folder. Replace the
/// default registration in DI to take full control of the response shape.
/// </summary>
/// <remarks>
/// Unlike the file and container builders, this builder is synchronous and does <em>not</em>
/// invoke the extension hook: the <c>CheckFolderInfo</c> endpoint calls
/// <see cref="IWopiHostExtensions.OnCheckFolderInfoAsync"/> after the builder returns.
/// Implementations only construct the default response.
/// </remarks>
public interface ICheckFolderInfoBuilder
{
    /// <summary>
    /// Builds the default <see cref="WopiCheckFolderInfo"/> for <paramref name="folder"/>.
    /// </summary>
    /// <param name="folder">The folder the response describes.</param>
    /// <param name="user">The authenticated principal. Drives the <c>IsAnonymousUser</c>
    /// response flag plus <c>UserId</c> and <c>UserFriendlyName</c>.</param>
    /// <returns>The default <c>CheckFolderInfo</c> response, before host customization.</returns>
    WopiCheckFolderInfo Build(IWopiContainer folder, ClaimsPrincipal user);
}
