using System.Security.Claims;
using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Context for the CheckFileInfo operation.
/// </summary>
/// <param name="User">the current user.</param>
/// <param name="File">the current file resource.</param>
/// <param name="CheckFileInfo">the default created <see cref="WopiCheckFileInfo"/></param>
public record WopiCheckFileInfoContext(ClaimsPrincipal? User, IWopiFile File, WopiCheckFileInfo CheckFileInfo);