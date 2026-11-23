using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Represents the context passed to <see cref="IWopiHostExtensions.OnCheckFolderInfoAsync"/> for the CheckFolderInfo operation.
/// </summary>
/// <param name="User">The current user, if any.</param>
/// <param name="Folder">The folder the response describes.</param>
/// <param name="CheckFolderInfo">The <see cref="WopiCheckFolderInfo"/> populated by the default builder.</param>
public record WopiCheckFolderInfoContext(ClaimsPrincipal? User, IWopiContainer Folder, WopiCheckFolderInfo CheckFolderInfo);
