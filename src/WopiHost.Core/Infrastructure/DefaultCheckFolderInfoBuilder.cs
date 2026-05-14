using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Default <see cref="ICheckFolderInfoBuilder"/>. Synchronous on purpose — see #363; the
/// controller fires <see cref="IWopiHostExtensions.OnCheckFolderInfoAsync"/> afterwards so its
/// only <c>await</c> on this path is the direct hook invocation, avoiding the Infer# null-deref
/// FP that the issue tracks.
/// </summary>
public class DefaultCheckFolderInfoBuilder : ICheckFolderInfoBuilder
{
    /// <inheritdoc />
    public WopiCheckFolderInfo Build(IWopiFolder folder, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(httpContext);

        var checkFolderInfo = new WopiCheckFolderInfo
        {
            FolderName = folder.Name,
        };

        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            checkFolderInfo.UserId = httpContext.User.GetUserId();
            checkFolderInfo.UserFriendlyName = httpContext.User.FindFirst(ClaimTypes.Name)?.Value;
        }
        else
        {
            checkFolderInfo.IsAnonymousUser = true;
        }

        return checkFolderInfo;
    }
}
