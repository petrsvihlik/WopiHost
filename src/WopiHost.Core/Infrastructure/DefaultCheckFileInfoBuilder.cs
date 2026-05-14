using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Default <see cref="ICheckFileInfoBuilder"/>. Populates the response from the file's
/// metadata, the supplied host capabilities, the principal's claims and permissions, then
/// fires <see cref="IWopiHostExtensions.OnCheckFileInfoAsync"/> for last-mile host customization.
/// </summary>
public class DefaultCheckFileInfoBuilder(
    IWopiPermissionProvider permissionProvider,
    IWopiHostExtensions extensions,
    IWopiWritableStorageProvider? writableStorageProvider = null,
    LinkGenerator? linkGenerator = null) : ICheckFileInfoBuilder
{
    /// <inheritdoc />
    public async Task<WopiCheckFileInfo> BuildAsync(
        IWopiFile file,
        HttpContext httpContext,
        WopiHostCapabilities? capabilities = null,
        string? userInfo = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(httpContext);

        // #181 make sure the BaseFileName always has an extension
        var baseFileName = file.Name.EndsWith(file.Extension, StringComparison.OrdinalIgnoreCase)
            ? file.Name
            : file.Name + "." + file.Extension.TrimStart('.');

        var checkFileInfo = new WopiCheckFileInfo
        {
            UserId = string.Empty,
            OwnerId = file.Owner.ToSafeIdentity(),
            Version = file.Version ?? file.LastWriteTimeUtc.ToString("s", CultureInfo.InvariantCulture),
            FileExtension = "." + file.Extension.TrimStart('.'),
            BaseFileName = baseFileName,
            LastModifiedTime = file.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture),
            Size = file.Exists ? file.Length : 0,
        };
        if (capabilities is not null)
        {
            checkFileInfo.SupportsCoauth = capabilities.SupportsCoauth;
            checkFileInfo.SupportsCobalt = capabilities.SupportsCobalt;
            checkFileInfo.SupportsFolders = capabilities.SupportsFolders;
            checkFileInfo.SupportsContainers = capabilities.SupportsContainers;
            checkFileInfo.SupportsLocks = capabilities.SupportsLocks;
            checkFileInfo.SupportsGetLock = capabilities.SupportsGetLock;
            checkFileInfo.SupportsExtendedLockLength = capabilities.SupportsExtendedLockLength;
            checkFileInfo.SupportsEcosystem = capabilities.SupportsEcosystem;
            checkFileInfo.SupportsGetFileWopiSrc = capabilities.SupportsGetFileWopiSrc;
            checkFileInfo.SupportedShareUrlTypes = capabilities.SupportedShareUrlTypes;
            checkFileInfo.SupportsScenarioLinks = capabilities.SupportsScenarioLinks;
            checkFileInfo.SupportsSecureStore = capabilities.SupportsSecureStore;
            checkFileInfo.SupportsFileCreation = capabilities.SupportsFileCreation;
            checkFileInfo.SupportsUpdate = capabilities.SupportsUpdate;
            checkFileInfo.SupportsRename = capabilities.SupportsRename;
            checkFileInfo.SupportsDeleteFile = capabilities.SupportsDeleteFile;
            checkFileInfo.SupportsUserInfo = capabilities.SupportsUserInfo;
        }

        checkFileInfo.FileNameMaxLength = writableStorageProvider?.FileNameMaxLength ?? 0;
        checkFileInfo.Sha256 = await file.GetEncodedSha256(cancellationToken).ConfigureAwait(false);

        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            checkFileInfo.UserId = httpContext.User.GetUserId();
            checkFileInfo.HostAuthenticationId = checkFileInfo.UserId;
            checkFileInfo.UserFriendlyName = httpContext.User.FindFirst(ClaimTypes.Name)?.Value;
            checkFileInfo.UserPrincipalName = httpContext.User.FindFirst(ClaimTypes.Upn)?.Value;

            var permissions = await permissionProvider.GetFilePermissionsAsync(httpContext.User, file, cancellationToken).ConfigureAwait(false);
            checkFileInfo.ReadOnly = permissions.HasFlag(WopiFilePermissions.ReadOnly);
            checkFileInfo.RestrictedWebViewOnly = permissions.HasFlag(WopiFilePermissions.RestrictedWebViewOnly);
            checkFileInfo.UserCanAttend = permissions.HasFlag(WopiFilePermissions.UserCanAttend);
            checkFileInfo.UserCanNotWriteRelative = capabilities?.SupportsUpdate == false || permissions.HasFlag(WopiFilePermissions.UserCanNotWriteRelative);
            checkFileInfo.UserCanPresent = permissions.HasFlag(WopiFilePermissions.UserCanPresent);
            checkFileInfo.UserCanRename = permissions.HasFlag(WopiFilePermissions.UserCanRename);
            checkFileInfo.UserCanWrite = permissions.HasFlag(WopiFilePermissions.UserCanWrite);
            checkFileInfo.WebEditingDisabled = permissions.HasFlag(WopiFilePermissions.WebEditingDisabled);
        }
        else
        {
            checkFileInfo.IsAnonymousUser = true;
        }

        // The UserInfo ... should be passed back to the WOPI client in subsequent CheckFileInfo responses in the UserInfo property.
        if (userInfo is not null)
        {
            checkFileInfo.UserInfo = userInfo;
        }

        // Populate a sensible default FileUrl pointing at this host's GetFile endpoint
        // with the request's access token in the query string. Per WOPI spec, FileUrl
        // must be a token-bearing URL that lets the client GET file content without
        // sending WOPI-specific headers. Hosts can override via IWopiHostExtensions
        // (e.g., to point at a CDN).
        if (linkGenerator is not null)
        {
            var (scheme, host, _, _, _) = httpContext.Request.GetProxyAwareUrlParts();
            if (!string.IsNullOrEmpty(scheme) && !string.IsNullOrEmpty(host))
            {
                // Preserve the file identifier's casing — the storage provider's lookup
                // is case-sensitive and the global LowercaseUrls=true would otherwise
                // mangle ids like "WOPITEST".
                var url = linkGenerator.GetUriByName(
                    WopiRouteNames.GetFile,
                    new
                    {
                        id = file.Identifier,
                        access_token = httpContext.Request.GetAccessToken(),
                    },
                    scheme,
                    new HostString(host),
                    pathBase: default,
                    fragment: default,
                    options: new LinkOptions { LowercaseUrls = false });
                if (url is not null)
                {
                    checkFileInfo.FileUrl = new Uri(url);
                }
            }
        }

        return await extensions.OnCheckFileInfoAsync(
            new WopiCheckFileInfoContext(httpContext.User, file, checkFileInfo),
            cancellationToken).ConfigureAwait(false);
    }
}
