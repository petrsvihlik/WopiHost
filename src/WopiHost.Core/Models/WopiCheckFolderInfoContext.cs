using System.Security.Claims;
using WopiHost.Abstractions;

namespace WopiHost.Core.Models;

/// <summary>
/// Context for the CheckFolderInfo operation.
/// </summary>
/// <param name="User">the current user.</param>
/// <param name="Folder">the current folder resource.</param>
/// <param name="CheckFolderInfo">the default created <see cref="WopiCheckFolderInfo"/></param>
public record WopiCheckFolderInfoContext(ClaimsPrincipal? User, IWopiFolder Folder, WopiCheckFolderInfo CheckFolderInfo);
