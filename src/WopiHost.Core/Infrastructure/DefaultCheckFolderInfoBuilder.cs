using System.Security.Claims;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Provides the default <see cref="ICheckFolderInfoBuilder"/> implementation, populated from the
/// folder's name and the principal's identity.
/// </summary>
/// <remarks>
/// Unlike the file and container builders, this builder does not fire
/// <see cref="IWopiHostExtensions.OnCheckFolderInfoAsync"/>; the caller does, after it returns.
/// </remarks>
public class DefaultCheckFolderInfoBuilder : ICheckFolderInfoBuilder
{
    /// <inheritdoc />
    public WopiCheckFolderInfo Build(IWopiContainer folder, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(user);

        var checkFolderInfo = new WopiCheckFolderInfo
        {
            FolderName = folder.Name,
        };

        if (user.Identity?.IsAuthenticated == true)
        {
            checkFolderInfo.UserId = user.GetUserId();
            checkFolderInfo.UserFriendlyName = user.FindFirst(ClaimTypes.Name)?.Value;
        }
        else
        {
            checkFolderInfo.IsAnonymousUser = true;
        }

        return checkFolderInfo;
    }
}
