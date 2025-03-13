using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Models;

namespace WopiHost.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="IWopiFile"/>.
/// </summary>
public static class WopiExtensions
{
    private static readonly SHA256 Sha = SHA256.Create();

    /// <summary>
    /// Returns base64 encoding of checksum (or calculates it from original contents if not provided)
    /// </summary>
    /// <param name="file">File object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>base64 encoded sha256 checksum</returns>
    public static async Task<string> GetEncodedSha256(this IWopiFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var result = file.Checksum;
        if (result is null)
        {
            using var stream = await file.GetReadStream(cancellationToken);
            result = await Sha.ComputeHashAsync(stream, cancellationToken);
        }
        return Convert.ToBase64String(result);
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
            checkFileInfo.UserPrincipalName = httpContext.User.FindFirst(ClaimTypes.Upn)?.Value ?? string.Empty;

            // try to parse permissions claims
            var securityHandler = httpContext.RequestServices.GetRequiredService<IWopiSecurityHandler>();
            var permissions = await securityHandler.GetUserPermissions(httpContext.User, file, cancellationToken);
            checkFileInfo.ReadOnly = permissions.HasFlag(WopiUserPermissions.ReadOnly);
            checkFileInfo.RestrictedWebViewOnly = permissions.HasFlag(WopiUserPermissions.RestrictedWebViewOnly);
            checkFileInfo.UserCanAttend = permissions.HasFlag(WopiUserPermissions.UserCanAttend);
            checkFileInfo.UserCanNotWriteRelative = capabilities?.SupportsUpdate == false || permissions.HasFlag(WopiUserPermissions.UserCanNotWriteRelative);
            checkFileInfo.UserCanPresent = permissions.HasFlag(WopiUserPermissions.UserCanPresent);
            checkFileInfo.UserCanRename = permissions.HasFlag(WopiUserPermissions.UserCanRename);
            checkFileInfo.UserCanWrite = permissions.HasFlag(WopiUserPermissions.UserCanWrite);
            checkFileInfo.WebEditingDisabled = permissions.HasFlag(WopiUserPermissions.WebEditingDisabled);
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
    /// <returns>WopiCheckContainerInfo</returns>
    public static async Task<WopiCheckContainerInfo> GetWopiCheckContainerInfo(
        this IWopiFolder container, 
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(container);

        var checkContainerInfo = new WopiCheckContainerInfo()
        {
            Name = container.Name,
            UserCanCreateChildContainer = httpContext.IsPermitted(Permission.Create),
            UserCanDelete = httpContext.IsPermitted(Permission.Delete),
            UserCanRename = httpContext.IsPermitted(Permission.Rename),
            UserCanCreateChildFile = httpContext.IsPermitted(Permission.CreateChildFile),
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
}
