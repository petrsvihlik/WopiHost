using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;

namespace WopiHost.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="IWopiFile"/>.
/// </summary>
public static class WopiExtensions
{
    /// <summary>
    /// Returns base64 encoding of checksum (or calculates it from original contents if not provided)
    /// </summary>
    /// <param name="file">File object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>base64 encoded sha256 checksum</returns>
    /// <remarks>
    /// Uses the static <see cref="SHA256.HashDataAsync(Stream, CancellationToken)"/> one-shot
    /// API rather than an instance-cached <see cref="SHA256"/>. A previous revision held a static
    /// <see cref="SHA256"/> instance and called <c>ComputeHashAsync</c> on it from concurrent
    /// CheckFileInfo requests; <see cref="SHA256"/> instance methods are not thread-safe, so that
    /// pattern could corrupt the hash or throw under load.
    /// </remarks>
    public static async Task<string> GetEncodedSha256(this IWopiFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var result = file.Checksum;
        if (result is null)
        {
            using var stream = await file.GetReadStream(cancellationToken);
            result = await SHA256.HashDataAsync(stream, cancellationToken);
        }
        return Convert.ToBase64String(result.Value.Span);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="WopiCheckFileInfo"/> based on the provided <see cref="IWopiFile"/> and <see cref="WopiHostCapabilities"/>.
    /// </summary>
    /// <param name="file"><see cref="IWopiFile"/> to return info</param>
    /// <param name="httpContext">current HttpContext</param>
    /// <param name="capabilities"><see cref="WopiHostCapabilities"/> to include in result</param>
    /// <param name="userInfo">additional user info</param>
    /// <param name="cancellationToken">cancellation token</param>
    public static async Task<WopiCheckFileInfo> GetWopiCheckFileInfo(
        this IWopiFile file, 
        HttpContext httpContext, 
        WopiHostCapabilities? capabilities = null,
        string? userInfo = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(httpContext);

        // #181 make sure the BaseFileName always has an extensions
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
            // Set host capabilities
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

        var writableStorageProvider = httpContext.RequestServices.GetService<IWopiWritableStorageProvider>();
        checkFileInfo.FileNameMaxLength = writableStorageProvider?.FileNameMaxLength ?? 0;
        checkFileInfo.Sha256 = await file.GetEncodedSha256(cancellationToken);

        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            checkFileInfo.UserId = httpContext.User.GetUserId();
            checkFileInfo.HostAuthenticationId = checkFileInfo.UserId;
            checkFileInfo.UserFriendlyName = httpContext.User.FindFirst(ClaimTypes.Name)?.Value;
            checkFileInfo.UserPrincipalName = httpContext.User.FindFirst(ClaimTypes.Upn)?.Value;

            var permissionProvider = httpContext.RequestServices.GetRequiredService<IWopiPermissionProvider>();
            var permissions = await permissionProvider.GetFilePermissionsAsync(httpContext.User, file, cancellationToken);
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
        // sending WOPI-specific headers. Hosts can override in OnCheckFileInfo (e.g.,
        // to point at a CDN).
        var linkGenerator = httpContext.RequestServices.GetService<LinkGenerator>();
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

        // allow changes and/or extensions before returning
        var wopiHostOptions = httpContext.RequestServices.GetService<IOptions<WopiHostOptions>>();
        if (wopiHostOptions is not null)
        {
            checkFileInfo = await wopiHostOptions.Value.OnCheckFileInfo(new WopiCheckFileInfoContext(httpContext.User, file, checkFileInfo));
        }

        return checkFileInfo;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="WopiCheckContainerInfo"/> based on the provided <see cref="IWopiFolder"/>
    /// </summary>
    /// <param name="container"><see cref="IWopiFolder"/> to return info</param>
    /// <param name="httpContext">current HttpContext</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>WopiCheckContainerInfo</returns>
    public static async Task<WopiCheckContainerInfo> GetWopiCheckContainerInfo(
        this IWopiFolder container,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(httpContext);

        var permissionProvider = httpContext.RequestServices.GetRequiredService<IWopiPermissionProvider>();
        var permissions = await permissionProvider.GetContainerPermissionsAsync(httpContext.User, container, cancellationToken);

        var checkContainerInfo = new WopiCheckContainerInfo()
        {
            Name = container.Name,
            UserCanCreateChildContainer = permissions.HasFlag(WopiContainerPermissions.UserCanCreateChildContainer),
            UserCanCreateChildFile = permissions.HasFlag(WopiContainerPermissions.UserCanCreateChildFile),
            UserCanDelete = permissions.HasFlag(WopiContainerPermissions.UserCanDelete),
            UserCanRename = permissions.HasFlag(WopiContainerPermissions.UserCanRename),
            IsEduUser = false,
        };

        // allow changes and/or extensions before returning
        var wopiHostOptions = httpContext.RequestServices.GetService<IOptions<WopiHostOptions>>();
        if (wopiHostOptions is not null)
        {
            checkContainerInfo = await wopiHostOptions.Value.OnCheckContainerInfo(new WopiCheckContainerInfoContext(httpContext.User, container, checkContainerInfo));
        }

        return checkContainerInfo;
    }

    /// <summary>
    /// Builds a default <see cref="WopiCheckFolderInfo"/> for the given <see cref="IWopiFolder"/>
    /// — folder name, plus user identity slots populated from <see cref="HttpContext.User"/>.
    /// Used by the OneNote-for-the-web Folders endpoint.
    /// </summary>
    /// <remarks>
    /// Synchronous on purpose — see #363. The <see cref="WopiHostOptions.OnCheckFolderInfo"/>
    /// callback is fired by the <c>FoldersController</c> after this returns, so the controller's
    /// only <c>await</c> in this path is the direct delegate invocation. An async extension
    /// method here would re-introduce the Infer# null-deref FP that the issue tracks.
    /// </remarks>
    /// <param name="folder"><see cref="IWopiFolder"/> to return info for.</param>
    /// <param name="httpContext">current <see cref="HttpContext"/>.</param>
    /// <returns>The default <see cref="WopiCheckFolderInfo"/> before any host-supplied callback.</returns>
    public static WopiCheckFolderInfo BuildCheckFolderInfo(
        this IWopiFolder folder,
        HttpContext httpContext)
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
