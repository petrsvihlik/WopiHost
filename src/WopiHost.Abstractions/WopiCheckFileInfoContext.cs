using System.Security.Claims;

namespace WopiHost.Abstractions;

/// <summary>
/// Represents the context passed to <see cref="IWopiHostExtensions.OnCheckFileInfoAsync"/> for the CheckFileInfo operation.
/// </summary>
/// <param name="User">The current user, if any.</param>
/// <param name="File">The file the response describes.</param>
/// <param name="CheckFileInfo">The <see cref="WopiCheckFileInfo"/> populated by the default builder.</param>
public record WopiCheckFileInfoContext(ClaimsPrincipal? User, IWopiFile File, WopiCheckFileInfo CheckFileInfo);
