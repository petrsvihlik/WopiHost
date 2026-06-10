using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Routing;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Default <see cref="ICheckFileInfoBuilder"/>. Populates the response from the file's
/// metadata, the supplied host capabilities, the principal's claims and permissions, then
/// fires <see cref="IWopiHostExtensions.OnCheckFileInfoAsync"/> for last-mile host customization.
/// </summary>
/// <remarks>
/// <paramref name="linkGenerator"/> is retained for binary compatibility only. Earlier versions
/// used it to populate a default <c>FileUrl</c> pointing back at this host's GetFile endpoint —
/// a URL WOPI clients fetch without proof signing (per spec), which the proof-validation filter
/// on that endpoint then rejects. The default is gone; see the note in
/// <see cref="BuildAsync"/>.
/// </remarks>
public class DefaultCheckFileInfoBuilder(
    IWopiPermissionProvider permissionProvider,
    IWopiHostExtensions extensions,
    IWopiWritableStorageProvider? writableStorageProvider = null,
#pragma warning disable CS9113 // Parameter is unread — kept for constructor binary compatibility.
    LinkGenerator? linkGenerator = null) : ICheckFileInfoBuilder
#pragma warning restore CS9113
{
    /// <inheritdoc />
    public async Task<WopiCheckFileInfo> BuildAsync(
        IWopiFile file,
        WopiRequestInfo request,
        WopiHostCapabilities? capabilities = null,
        string? userInfo = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(request);

        // Make sure the BaseFileName always has an extension.
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
            checkFileInfo.SupportsAddActivities = capabilities.SupportsAddActivities;
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

        if (request.User?.Identity?.IsAuthenticated == true)
        {
            checkFileInfo.UserId = request.User.GetUserId();
            checkFileInfo.HostAuthenticationId = checkFileInfo.UserId;
            checkFileInfo.UserFriendlyName = request.User.FindFirst(ClaimTypes.Name)?.Value;
            checkFileInfo.UserPrincipalName = request.User.FindFirst(ClaimTypes.Upn)?.Value;

            var permissions = await permissionProvider.GetFilePermissionsAsync(request.User, file, cancellationToken).ConfigureAwait(false);
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

        // FileUrl is deliberately left unset. Per the WOPI proof-keys spec, clients fetch
        // FileUrl WITHOUT signing the request ("Requests to the FileUrl aren't signed"), and
        // FileUrl-preferring clients (ONLYOFFICE) honour that. Every endpoint this host maps —
        // including GetFile — sits behind WopiOriginValidationEndpointFilter, so a default
        // FileUrl pointing back at the GetFile route is a URL the client cannot legally use:
        // the unsigned fetch 500s on proof validation and the document fails to open. Clients
        // that don't get a FileUrl fall back to the standard (signed) GetFile request against
        // the same route. Hosts with a genuinely unsigned download channel (CDN, pre-signed
        // blob URL) can still set FileUrl via IWopiHostExtensions.OnCheckFileInfoAsync.
        return await extensions.OnCheckFileInfoAsync(
            new WopiCheckFileInfoContext(request.User, file, checkFileInfo),
            cancellationToken).ConfigureAwait(false);
    }
}
