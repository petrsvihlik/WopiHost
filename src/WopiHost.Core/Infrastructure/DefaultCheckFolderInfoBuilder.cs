using System.Security.Claims;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Default <see cref="ICheckFolderInfoBuilder"/>. Synchronous on purpose: the caller fires
/// <see cref="IWopiHostExtensions.OnCheckFolderInfoAsync"/> afterwards so its only <c>await</c>
/// on this path is the direct hook invocation, which keeps static analysis of the async state
/// machine precise.
/// </summary>
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
