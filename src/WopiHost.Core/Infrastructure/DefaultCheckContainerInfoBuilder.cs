using Microsoft.AspNetCore.Http;
using WopiHost.Abstractions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Default <see cref="ICheckContainerInfoBuilder"/>. Populates the response from the
/// container's metadata and the principal's permissions, then fires
/// <see cref="IWopiHostExtensions.OnCheckContainerInfoAsync"/> for last-mile host customization.
/// </summary>
public class DefaultCheckContainerInfoBuilder(
    IWopiPermissionProvider permissionProvider,
    IWopiHostExtensions extensions) : ICheckContainerInfoBuilder
{
    /// <inheritdoc />
    public async Task<WopiCheckContainerInfo> BuildAsync(
        IWopiFolder container,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(httpContext);

        var permissions = await permissionProvider.GetContainerPermissionsAsync(httpContext.User, container, cancellationToken).ConfigureAwait(false);

        var checkContainerInfo = new WopiCheckContainerInfo
        {
            Name = container.Name,
            UserCanCreateChildContainer = permissions.HasFlag(WopiContainerPermissions.UserCanCreateChildContainer),
            UserCanCreateChildFile = permissions.HasFlag(WopiContainerPermissions.UserCanCreateChildFile),
            UserCanDelete = permissions.HasFlag(WopiContainerPermissions.UserCanDelete),
            UserCanRename = permissions.HasFlag(WopiContainerPermissions.UserCanRename),
            IsEduUser = false,
        };

        return await extensions.OnCheckContainerInfoAsync(
            new WopiCheckContainerInfoContext(httpContext.User, container, checkContainerInfo),
            cancellationToken).ConfigureAwait(false);
    }
}
