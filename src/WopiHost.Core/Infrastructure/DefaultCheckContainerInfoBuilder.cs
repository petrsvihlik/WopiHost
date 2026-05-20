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
        IWopiContainer container,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(httpContext);

        var permissions = await permissionProvider.GetContainerPermissionsAsync(httpContext.User, container, cancellationToken).ConfigureAwait(false);

        // Spec: IsAnonymousUser "should match the IsAnonymousUser value returned in
        // CheckFileInfo" — and the file / folder builders both set it from the auth state.
        // CheckContainerInfo was previously omitting it (left as default `false`), so
        // anonymous users were reported as authenticated in the container response.
        var isAnonymous = httpContext.User?.Identity?.IsAuthenticated != true;

        var checkContainerInfo = new WopiCheckContainerInfo
        {
            Name = container.Name,
            UserCanCreateChildContainer = permissions.HasFlag(WopiContainerPermissions.UserCanCreateChildContainer),
            UserCanCreateChildFile = permissions.HasFlag(WopiContainerPermissions.UserCanCreateChildFile),
            UserCanDelete = permissions.HasFlag(WopiContainerPermissions.UserCanDelete),
            UserCanRename = permissions.HasFlag(WopiContainerPermissions.UserCanRename),
            IsAnonymousUser = isAnonymous,
            IsEduUser = false,
        };

        return await extensions.OnCheckContainerInfoAsync(
            new WopiCheckContainerInfoContext(httpContext.User, container, checkContainerInfo),
            cancellationToken).ConfigureAwait(false);
    }
}
